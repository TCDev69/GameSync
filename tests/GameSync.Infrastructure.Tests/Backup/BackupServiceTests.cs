using FluentAssertions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Configuration;
using GameSync.Core.Models;
using GameSync.Infrastructure.Paths;
using GameSync.Infrastructure.Sync;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameSync.Infrastructure.Tests.Backup;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task CreateBackup_PreservesFileAndDirectoryStructure()
    {
        var root = CreateTempRoot();
        try
        {
            var paths = new TestPaths(root);
            paths.EnsureCreated();
            var sourceDir = Path.Combine(root, "savesource");
            Directory.CreateDirectory(Path.Combine(sourceDir, "nested"));
            File.WriteAllText(Path.Combine(sourceDir, "nested", "save.dat"), "data");
            var sourceFile = Path.Combine(root, "settings.dat");
            File.WriteAllText(sourceFile, "cfg");

            var store = new FixedMachineStore(new MachineConfiguration
            {
                MachineId = "TEST",
                Backup = new BackupSettings { Enabled = true, MaxBackupsPerGame = 10 }
            });

            var service = new BackupService(paths, new PathResolver(), store, NullLogger<BackupService>.Instance);
            var backup = await service.CreateBackupAsync("demo_game", [sourceDir, sourceFile]);

            Directory.Exists(backup).Should().BeTrue();
            var dirs = Directory.GetDirectories(backup);
            dirs.Should().HaveCount(2);
            dirs.Any(d => File.Exists(Path.Combine(d, "nested", "save.dat"))).Should().BeTrue();
            dirs.Any(d => File.Exists(Path.Combine(d, "settings.dat"))).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Prune_RetainsConfiguredMaximum()
    {
        var root = CreateTempRoot();
        try
        {
            var paths = new TestPaths(root);
            paths.EnsureCreated();
            var gameBackupRoot = Path.Combine(paths.BackupsDirectory, "demo_game");
            for (var i = 0; i < 5; i++)
            {
                Directory.CreateDirectory(Path.Combine(gameBackupRoot, $"2026-08-09_10-0{i}-00"));
                await Task.Delay(5);
            }

            var store = new FixedMachineStore(new MachineConfiguration
            {
                MachineId = "TEST",
                Backup = new BackupSettings { Enabled = true, MaxBackupsPerGame = 2 }
            });

            var service = new BackupService(paths, new PathResolver(), store, NullLogger<BackupService>.Instance);
            await service.PruneAsync("demo_game");

            (await service.ListBackupsAsync("demo_game")).Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), "GameSyncBackupTests", Guid.NewGuid().ToString("N"));

    private sealed class TestPaths : ILocalAppDataPaths
    {
        public TestPaths(string root)
        {
            Root = root;
            MachineConfigurationFile = Path.Combine(root, "machine.json");
            RepositoriesDirectory = Path.Combine(root, "repositories");
            CacheDirectory = Path.Combine(root, "cache");
            LogsDirectory = Path.Combine(root, "logs");
            BackupsDirectory = Path.Combine(root, "backups");
            UpdatesDirectory = Path.Combine(root, "updates");
        }

        public string Root { get; }
        public string MachineConfigurationFile { get; }
        public string RepositoriesDirectory { get; }
        public string CacheDirectory { get; }
        public string LogsDirectory { get; }
        public string BackupsDirectory { get; }
        public string UpdatesDirectory { get; }
        public void EnsureCreated()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(RepositoriesDirectory);
            Directory.CreateDirectory(CacheDirectory);
            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(BackupsDirectory);
        }
    }

    private sealed class FixedMachineStore : IMachineConfigurationStore
    {
        private MachineConfiguration _config;
        public FixedMachineStore(MachineConfiguration config) => _config = config;
        public Task<MachineConfiguration> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_config);
        public Task SaveAsync(MachineConfiguration configuration, CancellationToken cancellationToken = default)
        {
            _config = configuration;
            return Task.CompletedTask;
        }
    }
}
