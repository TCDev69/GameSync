using FluentAssertions;
using GameSync.Core.Configuration;
using GameSync.Core.Models;
using GameSync.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameSync.Infrastructure.Tests.Configuration;

public sealed class JsonConfigurationStoreTests
{
    [Fact]
    public async Task MachineConfiguration_RoundTrips()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncTests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new TestLocalAppDataPaths(root);
            var validator = new ConfigurationValidator();
            var store = new JsonMachineConfigurationStore(paths, validator, NullLogger<JsonMachineConfigurationStore>.Instance);

            var config = new MachineConfiguration
            {
                MachineId = "TEST-PC",
                Games =
                {
                    ["minecraft"] = new GameLaunchConfiguration
                    {
                        Executable = @"C:\Games\Minecraft\Minecraft.exe",
                        Arguments = "--demo"
                    }
                }
            };

            await store.SaveAsync(config);
            var loaded = await store.LoadAsync();

            loaded.MachineId.Should().Be("TEST-PC");
            loaded.Games.Should().ContainKey("minecraft");
            loaded.Games["minecraft"].Executable.Should().Contain("Minecraft.exe");
            loaded.Games["minecraft"].Arguments.Should().Be("--demo");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SharedGamesConfiguration_RoundTrips()
    {
        var repo = Path.Combine(Path.GetTempPath(), "GameSyncRepoTests", Guid.NewGuid().ToString("N"));
        try
        {
            var validator = new ConfigurationValidator();
            var store = new JsonSharedGamesConfigurationStore(validator, NullLogger<JsonSharedGamesConfigurationStore>.Instance);

            var config = new GamesConfiguration
            {
                Games =
                [
                    new Game
                    {
                        Id = "cyberpunk_2077",
                        Title = "Cyberpunk 2077",
                        CoverUrl = "https://example.com/cover.jpg",
                        SaveLocations =
                        [
                            new SaveLocation
                            {
                                Id = "main",
                                Type = SaveLocationType.Directory,
                                RemotePath = "saves/cyberpunk_2077/main",
                                LocalPath = "%USERPROFILE%/Saved Games/CD Projekt Red/Cyberpunk 2077"
                            }
                        ]
                    }
                ]
            };

            await store.SaveAsync(repo, config);
            var loaded = await store.LoadAsync(repo);

            loaded.SchemaVersion.Should().Be(1);
            loaded.Games.Should().HaveCount(1);
            loaded.Games[0].Id.Should().Be("cyberpunk_2077");
            loaded.Games[0].SaveLocations[0].LocalPath.Should().Contain("%USERPROFILE%");
            File.Exists(Path.Combine(repo, "config", "games.json")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(repo))
            {
                Directory.Delete(repo, recursive: true);
            }
        }
    }

    private sealed class TestLocalAppDataPaths : Core.Abstractions.Storage.ILocalAppDataPaths
    {
        public TestLocalAppDataPaths(string root)
        {
            Root = root;
            MachineConfigurationFile = Path.Combine(root, "machine.json");
            RepositoriesDirectory = Path.Combine(root, "repositories");
            CacheDirectory = Path.Combine(root, "cache");
            LogsDirectory = Path.Combine(root, "logs");
            BackupsDirectory = Path.Combine(root, "backups");
        }

        public string Root { get; }
        public string MachineConfigurationFile { get; }
        public string RepositoriesDirectory { get; }
        public string CacheDirectory { get; }
        public string LogsDirectory { get; }
        public string BackupsDirectory { get; }

        public void EnsureCreated()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(RepositoriesDirectory);
            Directory.CreateDirectory(CacheDirectory);
            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(BackupsDirectory);
        }
    }
}
