using FluentAssertions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Git;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Abstractions.Repository;
using GameSync.Core.Configuration;
using GameSync.Core.Errors;
using GameSync.Core.Models;
using GameSync.Infrastructure.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GameSync.Infrastructure.Tests.GitHub;

public sealed class GitHubRepositoryConnectionServiceTests
{
    [Fact]
    public async Task ConnectRepository_ClonesInitializesAndPersistsSelection()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncConnect", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var selected = new RepositoryConfiguration
            {
                Owner = "me",
                Name = "gamesync-saves",
                DefaultBranch = "main",
                CloneUrl = "https://github.com/me/gamesync-saves.git",
                LocalPath = Path.Combine(root, "repo")
            };

            var gitHub = Substitute.For<IGitHubService>();
            gitHub.GetRepositoryAsync("me", "gamesync-saves", Arg.Any<CancellationToken>()).Returns(selected);

            var repoService = Substitute.For<IRepositoryService>();
            repoService.GetLocalRepositoryPath("me", "gamesync-saves").Returns(selected.LocalPath!);
            repoService.EnsureLocalRepositoryAsync(Arg.Any<RepositoryConfiguration>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var cfg = ci.ArgAt<RepositoryConfiguration>(0);
                    Directory.CreateDirectory(cfg.LocalPath!);
                    return cfg;
                });

            var gamesStore = Substitute.For<ISharedGamesConfigurationStore>();
            gamesStore.GetConfigurationRelativePath().Returns("config/games.json");
            gamesStore.SaveAsync(Arg.Any<string>(), Arg.Any<GamesConfiguration>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            gamesStore.When(x => x.SaveAsync(Arg.Any<string>(), Arg.Any<GamesConfiguration>(), Arg.Any<CancellationToken>()))
                .Do(ci =>
                {
                    var path = Path.Combine(ci.ArgAt<string>(0), "config", "games.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, """{"schemaVersion":1,"games":[]}""");
                });
            gamesStore.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new GamesConfiguration());

            MachineConfiguration? saved = null;
            var machineStore = Substitute.For<IMachineConfigurationStore>();
            machineStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(new MachineConfiguration { MachineId = "PC" });
            machineStore.SaveAsync(Arg.Any<MachineConfiguration>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    saved = ci.ArgAt<MachineConfiguration>(0);
                    return Task.CompletedTask;
                });

            var service = new GitHubRepositoryConnectionService(
                gitHub,
                repoService,
                gamesStore,
                machineStore,
                new ConfigurationValidator(),
                Substitute.For<IGitService>(),
                NullLogger<GitHubRepositoryConnectionService>.Instance);

            var result = await service.ConnectRepositoryAsync(selected);

            result.Succeeded.Should().BeTrue();
            result.InitializedStructure.Should().BeTrue();
            saved!.Repository.Should().NotBeNull();
            saved.Repository!.Name.Should().Be("gamesync-saves");
            await gitHub.Received(1).VerifyRepositoryAccessAsync("me", "gamesync-saves", Arg.Any<CancellationToken>());
            await repoService.Received(1).EnsureLocalRepositoryAsync(Arg.Any<RepositoryConfiguration>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task ConnectRepository_InvalidRepository_FailsValidation()
    {
        var service = new GitHubRepositoryConnectionService(
            Substitute.For<IGitHubService>(),
            Substitute.For<IRepositoryService>(),
            Substitute.For<ISharedGamesConfigurationStore>(),
            Substitute.For<IMachineConfigurationStore>(),
            new ConfigurationValidator(),
            Substitute.For<IGitService>(),
            NullLogger<GitHubRepositoryConnectionService>.Instance);

        var result = await service.ConnectRepositoryAsync(new RepositoryConfiguration
        {
            Owner = "me",
            Name = "../evil",
            DefaultBranch = "main"
        });

        result.Succeeded.Should().BeFalse();
        result.Error.Should().BeOfType<RepositoryUnavailableException>();
    }

    [Fact]
    public async Task ConnectRepository_IncompatibleGamesJson_FailsClearly()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncBadRepo", Guid.NewGuid().ToString("N"));
        var repoPath = Path.Combine(root, "repo");
        Directory.CreateDirectory(Path.Combine(repoPath, "config"));
        File.WriteAllText(Path.Combine(repoPath, "config", "games.json"), """{"schemaVersion":1,"games":[{"id":"BAD ID","title":"x","saveLocations":[]}]}""");

        try
        {
            var selected = new RepositoryConfiguration
            {
                Owner = "me",
                Name = "gamesync-saves",
                DefaultBranch = "main",
                LocalPath = repoPath,
                CloneUrl = "https://github.com/me/gamesync-saves.git"
            };

            var gitHub = Substitute.For<IGitHubService>();
            gitHub.GetRepositoryAsync("me", "gamesync-saves", Arg.Any<CancellationToken>()).Returns(selected);

            var repoService = Substitute.For<IRepositoryService>();
            repoService.EnsureLocalRepositoryAsync(Arg.Any<RepositoryConfiguration>(), Arg.Any<CancellationToken>())
                .Returns(selected);

            var gamesStore = Substitute.For<ISharedGamesConfigurationStore>();
            gamesStore.GetConfigurationRelativePath().Returns("config/games.json");
            gamesStore.LoadAsync(repoPath, Arg.Any<CancellationToken>())
                .Returns<GamesConfiguration>(_ => throw new ConfigurationValidationException(["bad id"]));

            var service = new GitHubRepositoryConnectionService(
                gitHub,
                repoService,
                gamesStore,
                Substitute.For<IMachineConfigurationStore>(),
                new ConfigurationValidator(),
                Substitute.For<IGitService>(),
                NullLogger<GitHubRepositoryConnectionService>.Instance);

            var result = await service.ConnectRepositoryAsync(selected);
            result.Succeeded.Should().BeFalse();
            result.Error.Should().BeOfType<RepositoryIncompatibleException>();
            result.Message.Should().Contain("incompatible");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ConnectRepository_OfflineVerify_DoesNotDeleteLocalData()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncOffline", Guid.NewGuid().ToString("N"));
        var marker = Path.Combine(root, "keep-me.txt");
        Directory.CreateDirectory(root);
        File.WriteAllText(marker, "local");

        try
        {
            var gitHub = Substitute.For<IGitHubService>();
            gitHub.VerifyRepositoryAccessAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new GitHubUnavailableException("offline")));

            var service = new GitHubRepositoryConnectionService(
                gitHub,
                Substitute.For<IRepositoryService>(),
                Substitute.For<ISharedGamesConfigurationStore>(),
                Substitute.For<IMachineConfigurationStore>(),
                new ConfigurationValidator(),
                Substitute.For<IGitService>(),
                NullLogger<GitHubRepositoryConnectionService>.Instance);

            var result = await service.ConnectRepositoryAsync(new RepositoryConfiguration
            {
                Owner = "me",
                Name = "gamesync-saves",
                DefaultBranch = "main",
                LocalPath = root
            });

            result.Succeeded.Should().BeFalse();
            File.Exists(marker).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
