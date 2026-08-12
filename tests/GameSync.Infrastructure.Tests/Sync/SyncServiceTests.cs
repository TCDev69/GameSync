using FluentAssertions;
using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Git;
using GameSync.Core.Abstractions.Repository;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Configuration;
using GameSync.Core.Models;
using GameSync.Infrastructure.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GameSync.Infrastructure.Tests.Sync;

public sealed class SyncServiceTests
{
    [Fact]
    public async Task SyncBeforeGameLaunch_RestoresWhenNoConflicts()
    {
        var game = CreateGame(@"C:\tmp\local");
        var machine = new MachineConfiguration
        {
            MachineId = "DESKTOP",
            Repository = new RepositoryConfiguration
            {
                Owner = "me",
                Name = "saves",
                LocalPath = @"C:\tmp\repo"
            }
        };
        var games = new GamesConfiguration { Games = [game] };

        var machineStore = Substitute.For<IMachineConfigurationStore>();
        machineStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(machine);
        var gamesStore = Substitute.For<ISharedGamesConfigurationStore>();
        gamesStore.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(games);
        var repoService = Substitute.For<IRepositoryService>();
        repoService.IsLocalRepositoryReadyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        repoService.GetLocalRepositoryPath(Arg.Any<string>(), Arg.Any<string>()).Returns(@"C:\tmp\repo");

        var git = Substitute.For<IGitService>();
        git.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new GitRepository
        {
            LocalPath = @"C:\tmp\repo",
            HasUncommittedChanges = false,
            SyncStatus = SyncStatus.UpToDate
        });
        git.GetConflictsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Conflict>());

        var save = Substitute.For<ISaveService>();
        save.DetectChangesAsync(Arg.Any<Game>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SaveChangesDetected.Empty);
        save.HasRepositorySaveContent(Arg.Any<Game>(), Arg.Any<string>()).Returns(true);
        save.HasLocalSaveContent(Arg.Any<Game>()).Returns(false);
        var backup = Substitute.For<IBackupService>();
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.Resolve(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));
        pathResolver.IsAllowedLocalSaveTarget(Arg.Any<string>()).Returns(true);

        var sync = new SyncService(
            machineStore,
            gamesStore,
            new ConfigurationValidator(),
            repoService,
            git,
            save,
            backup,
            pathResolver,
            NullLogger<SyncService>.Instance);

        var result = await sync.SyncBeforeGameLaunchAsync("demo_game");

        result.Succeeded.Should().BeTrue();
        await git.Received(1).FetchAsync(@"C:\tmp\repo", Arg.Any<CancellationToken>());
        await git.Received(1).PullAsync(@"C:\tmp\repo", Arg.Any<CancellationToken>());
        await save.Received(1).RestoreRepositoryToLocalAsync(game, @"C:\tmp\repo", true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncBeforeGameLaunch_AllowsFirstLaunchWhenRemoteHasNoSaves()
    {
        var game = CreateGame(@"C:\tmp\local");
        var machine = new MachineConfiguration
        {
            MachineId = "DESKTOP",
            Repository = new RepositoryConfiguration
            {
                Owner = "me",
                Name = "saves",
                LocalPath = @"C:\tmp\repo"
            }
        };
        var games = new GamesConfiguration { Games = [game] };

        var machineStore = Substitute.For<IMachineConfigurationStore>();
        machineStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(machine);
        var gamesStore = Substitute.For<ISharedGamesConfigurationStore>();
        gamesStore.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(games);
        var repoService = Substitute.For<IRepositoryService>();
        repoService.IsLocalRepositoryReadyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var git = Substitute.For<IGitService>();
        git.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new GitRepository
        {
            LocalPath = @"C:\tmp\repo",
            HasUncommittedChanges = false,
            SyncStatus = SyncStatus.UpToDate
        });
        git.GetConflictsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Conflict>());

        var save = Substitute.For<ISaveService>();
        save.DetectChangesAsync(Arg.Any<Game>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SaveChangesDetected { AddedFiles = ["main/slot.sav"] });
        save.HasRepositorySaveContent(Arg.Any<Game>(), Arg.Any<string>()).Returns(false);
        save.HasLocalSaveContent(Arg.Any<Game>()).Returns(true);

        var sync = new SyncService(
            machineStore,
            gamesStore,
            new ConfigurationValidator(),
            repoService,
            git,
            save,
            Substitute.For<IBackupService>(),
            Substitute.For<IPathResolver>(),
            NullLogger<SyncService>.Instance);

        var result = await sync.SyncBeforeGameLaunchAsync("demo_game");

        result.Succeeded.Should().BeTrue();
        result.Message.Should().Contain("No remote saves");
        await save.DidNotReceive().RestoreRepositoryToLocalAsync(
            Arg.Any<Game>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncBeforeGameLaunch_RefusesWhenLocalSavesDiverge()
    {
        var game = CreateGame(@"C:\tmp\local");
        var machine = new MachineConfiguration
        {
            MachineId = "DESKTOP",
            Repository = new RepositoryConfiguration
            {
                Owner = "me",
                Name = "saves",
                LocalPath = @"C:\tmp\repo"
            }
        };
        var games = new GamesConfiguration { Games = [game] };

        var machineStore = Substitute.For<IMachineConfigurationStore>();
        machineStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(machine);
        var gamesStore = Substitute.For<ISharedGamesConfigurationStore>();
        gamesStore.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(games);
        var repoService = Substitute.For<IRepositoryService>();
        repoService.IsLocalRepositoryReadyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var git = Substitute.For<IGitService>();
        git.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new GitRepository
        {
            LocalPath = @"C:\tmp\repo",
            HasUncommittedChanges = false,
            SyncStatus = SyncStatus.UpToDate
        });
        git.GetConflictsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Conflict>());

        var save = Substitute.For<ISaveService>();
        save.DetectChangesAsync(Arg.Any<Game>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SaveChangesDetected { ChangedFiles = ["main/slot.sav"] });
        save.HasRepositorySaveContent(Arg.Any<Game>(), Arg.Any<string>()).Returns(true);
        save.HasLocalSaveContent(Arg.Any<Game>()).Returns(true);

        var sync = new SyncService(
            machineStore,
            gamesStore,
            new ConfigurationValidator(),
            repoService,
            git,
            save,
            Substitute.For<IBackupService>(),
            Substitute.For<IPathResolver>(),
            NullLogger<SyncService>.Instance);

        var result = await sync.SyncBeforeGameLaunchAsync("demo_game");

        result.Succeeded.Should().BeFalse();
        result.Status.Should().Be(SyncStatus.Conflicted);
        await save.DidNotReceive().RestoreRepositoryToLocalAsync(
            Arg.Any<Game>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAfterGameExit_SkipsWhenNoChanges()
    {
        var game = CreateGame(@"C:\tmp\local");
        var machine = new MachineConfiguration
        {
            MachineId = "DESKTOP",
            Repository = new RepositoryConfiguration
            {
                Owner = "me",
                Name = "saves",
                LocalPath = @"C:\tmp\repo"
            }
        };

        var machineStore = Substitute.For<IMachineConfigurationStore>();
        machineStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(machine);
        var gamesStore = Substitute.For<ISharedGamesConfigurationStore>();
        gamesStore.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new GamesConfiguration { Games = [game] });
        var repoService = Substitute.For<IRepositoryService>();
        repoService.IsLocalRepositoryReadyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var save = Substitute.For<ISaveService>();
        save.DetectChangesAsync(Arg.Any<Game>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SaveChangesDetected.Empty);

        var git = Substitute.For<IGitService>();
        var sync = new SyncService(
            machineStore,
            gamesStore,
            new ConfigurationValidator(),
            repoService,
            git,
            save,
            Substitute.For<IBackupService>(),
            Substitute.For<IPathResolver>(),
            NullLogger<SyncService>.Instance);

        var result = await sync.SyncAfterGameExitAsync("demo_game");
        result.Succeeded.Should().BeTrue();
        result.Message.Should().Contain("No save changes");
        await git.DidNotReceive().PushAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static Game CreateGame(string localPath) => new()
    {
        Id = "demo_game",
        Title = "Demo Game",
        SaveLocations =
        [
            new SaveLocation
            {
                Id = "main",
                Type = SaveLocationType.Directory,
                LocalPath = localPath,
                RemotePath = "saves/demo_game/main"
            }
        ]
    };
}
