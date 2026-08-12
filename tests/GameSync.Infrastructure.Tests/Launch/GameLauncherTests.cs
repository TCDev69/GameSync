using FluentAssertions;
using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Launch;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Configuration;
using GameSync.Core.Errors;
using GameSync.Core.Models;
using GameSync.Infrastructure.Launch;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GameSync.Infrastructure.Tests.Launch;

public sealed class GameLauncherTests
{
    [Fact]
    public async Task Launch_MissingGame_Fails()
    {
        var launcher = CreateLauncher(
            machine: ConfiguredMachine(),
            games: new GamesConfiguration(),
            out _,
            out _);

        var result = await launcher.LaunchAsync("missing_game");
        result.Succeeded.Should().BeFalse();
        result.Error.Should().BeOfType<ConfigurationValidationException>();
    }

    [Fact]
    public async Task Launch_MissingExecutable_FailsWithGameAndPath()
    {
        var machine = ConfiguredMachine();
        machine.Games["demo_game"] = new GameLaunchConfiguration
        {
            Executable = @"C:\Games\Missing\Game.exe"
        };

        var launcher = CreateLauncher(machine, DemoGames(), out _, out _);
        var result = await launcher.LaunchAsync("demo_game");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().BeOfType<GameExecutableNotFoundException>();
        var ex = (GameExecutableNotFoundException)result.Error!;
        ex.GameId.Should().Be("demo_game");
        ex.ExecutablePath.Should().Contain("Game.exe");
    }

    [Fact]
    public async Task Launch_PreSyncFailure_DoesNotStartProcess()
    {
        var root = CreateTempExe(out var exePath);
        try
        {
            var machine = ConfiguredMachine();
            machine.Games["demo_game"] = new GameLaunchConfiguration { Executable = exePath };

            var launcher = CreateLauncher(
                machine,
                DemoGames(),
                out var sync,
                out var processLauncher,
                preSync: SyncResult.Failure(SyncStatus.Conflicted, "conflict", "demo_game"));

            var result = await launcher.LaunchAsync("demo_game");

            result.Succeeded.Should().BeFalse();
            result.PreLaunchSync!.Succeeded.Should().BeFalse();
            await processLauncher.DidNotReceiveWithAnyArgs().StartAsync(default!, default);
            await sync.DidNotReceiveWithAnyArgs().SyncAfterGameExitAsync(default!, default);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Launch_Success_RunsBeforeStartWaitAfter()
    {
        var root = CreateTempExe(out var exePath);
        try
        {
            var machine = ConfiguredMachine();
            machine.Games["demo_game"] = new GameLaunchConfiguration
            {
                Executable = exePath,
                Arguments = "--fullscreen",
                WorkingDirectory = root
            };

            var launched = Substitute.For<ILaunchedProcess>();
            launched.Id.Returns(4242);
            launched.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(0);

            var launcher = CreateLauncher(
                machine,
                DemoGames(),
                out var sync,
                out var processLauncher,
                preSync: SyncResult.Success(SyncStatus.UpToDate, "ok", "demo_game"),
                postSync: SyncResult.Success(SyncStatus.UpToDate, "pushed", "demo_game"),
                launched: launched);

            var phases = new List<LaunchPhase>();
            var progress = new Progress<LaunchProgress>(p => phases.Add(p.Phase));
            var result = await launcher.LaunchAsync("demo_game", progress);

            result.Succeeded.Should().BeTrue();
            result.ProcessId.Should().Be(4242);
            result.ExitCode.Should().Be(0);
            await sync.Received(1).SyncBeforeGameLaunchAsync("demo_game", Arg.Any<CancellationToken>());
            await processLauncher.Received(1).StartAsync(
                Arg.Is<ProcessStartRequest>(r => r.ExecutablePath == exePath && r.Arguments == "--fullscreen"),
                Arg.Any<CancellationToken>());
            await sync.Received(1).SyncAfterGameExitAsync("demo_game", Arg.Any<CancellationToken>());
            phases.Should().Contain(LaunchPhase.GameRunning);
            phases.Should().Contain(LaunchPhase.Completed);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static GameLauncher CreateLauncher(
        MachineConfiguration machine,
        GamesConfiguration games,
        out ISyncService sync,
        out IProcessLauncher processLauncher,
        SyncResult? preSync = null,
        SyncResult? postSync = null,
        ILaunchedProcess? launched = null)
    {
        var machineStore = Substitute.For<IMachineConfigurationStore>();
        machineStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(machine);

        var gamesStore = Substitute.For<ISharedGamesConfigurationStore>();
        gamesStore.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(games);

        sync = Substitute.For<ISyncService>();
        sync.SyncBeforeGameLaunchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(preSync ?? SyncResult.Success(SyncStatus.UpToDate));
        sync.SyncAfterGameExitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(postSync ?? SyncResult.Success(SyncStatus.UpToDate));

        processLauncher = Substitute.For<IProcessLauncher>();
        processLauncher.StartAsync(Arg.Any<ProcessStartRequest>(), Arg.Any<CancellationToken>())
            .Returns(launched ?? Substitute.For<ILaunchedProcess>());

        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.Resolve(Arg.Any<string>()).Returns(ci => Path.GetFullPath(ci.ArgAt<string>(0)));

        var localPaths = Substitute.For<ILocalAppDataPaths>();
        localPaths.RepositoriesDirectory.Returns(Path.Combine(Path.GetTempPath(), "GameSyncTest", "repositories"));
        localPaths.BackupsDirectory.Returns(Path.Combine(Path.GetTempPath(), "GameSyncTest", "backups"));
        localPaths.CacheDirectory.Returns(Path.Combine(Path.GetTempPath(), "GameSyncTest", "cache"));

        return new GameLauncher(
            machineStore,
            gamesStore,
            sync,
            processLauncher,
            Substitute.For<IProtocolLauncher>(),
            Substitute.For<IGameProcessWatcher>(),
            Substitute.For<IGameSessionAwaiter>(),
            pathResolver,
            localPaths,
            NullLogger<GameLauncher>.Instance);
    }

    private static MachineConfiguration ConfiguredMachine() => new()
    {
        MachineId = "TEST",
        Repository = new RepositoryConfiguration
        {
            Owner = "me",
            Name = "saves",
            LocalPath = @"C:\tmp\repo"
        }
    };

    private static GamesConfiguration DemoGames() => new()
    {
        Games =
        [
            new Game
            {
                Id = "demo_game",
                Title = "Demo Game",
                SaveLocations =
                [
                    new SaveLocation
                    {
                        Id = "main",
                        Type = SaveLocationType.Directory,
                        LocalPath = "%USERPROFILE%/Saves",
                        RemotePath = "saves/demo_game/main"
                    }
                ]
            }
        ]
    };

    private static string CreateTempExe(out string exePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncLaunchTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        exePath = Path.Combine(root, "Game.exe");
        File.WriteAllText(exePath, "fake");
        return root;
    }
}
