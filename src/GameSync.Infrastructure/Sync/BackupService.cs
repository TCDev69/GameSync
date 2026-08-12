using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Abstractions.Sync;
using GameSync.Infrastructure.IO;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Sync;

public sealed class BackupService : IBackupService
{
    private readonly ILocalAppDataPaths _paths;
    private readonly IPathResolver _pathResolver;
    private readonly IMachineConfigurationStore _machineConfigurationStore;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        ILocalAppDataPaths paths,
        IPathResolver pathResolver,
        IMachineConfigurationStore machineConfigurationStore,
        ILogger<BackupService> logger)
    {
        _paths = paths;
        _pathResolver = pathResolver;
        _machineConfigurationStore = machineConfigurationStore;
        _logger = logger;
    }

    public async Task<string> CreateBackupAsync(string gameId, IEnumerable<string> sourcePaths, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentNullException.ThrowIfNull(sourcePaths);

        _paths.EnsureCreated();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var backupRoot = Path.Combine(_paths.BackupsDirectory, Sanitize(gameId), timestamp);
        Directory.CreateDirectory(backupRoot);

        var index = 0;
        foreach (var source in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            var resolved = source.Contains('%', StringComparison.Ordinal)
                ? _pathResolver.Resolve(source)
                : _pathResolver.Normalize(source);
            if (File.Exists(resolved))
            {
                var destDir = Path.Combine(backupRoot, $"item_{index}");
                Directory.CreateDirectory(destDir);
                var dest = Path.Combine(destDir, Path.GetFileName(resolved));
                AtomicFile.Copy(resolved, dest, cancellationToken);
                WriteMeta(destDir, resolved, isDirectory: false);
            }
            else if (Directory.Exists(resolved))
            {
                var destDir = Path.Combine(backupRoot, $"item_{index}");
                CopyDirectory(resolved, destDir, cancellationToken);
                WriteMeta(destDir, resolved, isDirectory: true);
            }
            else
            {
                _logger.LogWarning("Backup skipped missing path {Path} for game {GameId}", resolved, gameId);
                continue;
            }

            index++;
        }

        _logger.LogInformation("Created backup for game {GameId} at {BackupRoot}", gameId, backupRoot);

        var machine = await _machineConfigurationStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (machine.Backup.Enabled)
        {
            await PruneAsync(gameId, machine.Backup.MaxBackupsPerGame, cancellationToken).ConfigureAwait(false);
        }

        return backupRoot;
    }

    public async Task PruneAsync(string gameId, int? maxBackupsOverride = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

        var max = maxBackupsOverride;
        if (max is null)
        {
            var machine = await _machineConfigurationStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!machine.Backup.Enabled)
            {
                return;
            }

            max = machine.Backup.MaxBackupsPerGame;
        }

        if (max <= 0)
        {
            return;
        }

        var backups = (await ListBackupsAsync(gameId, cancellationToken).ConfigureAwait(false))
            .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var obsolete in backups.Skip(max.Value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Directory.Delete(obsolete, recursive: true);
                _logger.LogInformation("Pruned backup {BackupPath}", obsolete);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to prune backup {BackupPath}", obsolete);
            }
        }
    }

    public Task<IReadOnlyList<string>> ListBackupsAsync(string gameId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        cancellationToken.ThrowIfCancellationRequested();

        var gameRoot = Path.Combine(_paths.BackupsDirectory, Sanitize(gameId));
        if (!Directory.Exists(gameRoot))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var list = Directory.GetDirectories(gameRoot)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyList<string>>(list);
    }

    private static void WriteMeta(string destDir, string originalPath, bool isDirectory)
    {
        var meta = $"originalPath={originalPath}{Environment.NewLine}isDirectory={isDirectory}{Environment.NewLine}";
        File.WriteAllText(Path.Combine(destDir, ".gamesync-backup-meta"), meta);
    }

    private static void CopyDirectory(string sourceDir, string destDir, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destDir);
        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(destDir, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            AtomicFile.Copy(file, dest, cancellationToken);
        }
    }

    private static string Sanitize(string gameId)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            gameId = gameId.Replace(c, '_');
        }

        return gameId;
    }
}
