using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Errors;
using GameSync.Core.Models;
using GameSync.Core.Services;
using GameSync.Infrastructure.IO;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Sync;

public sealed class SaveService : ISaveService, ISaveSyncService
{
    private readonly IPathResolver _pathResolver;
    private readonly IBackupService _backupService;
    private readonly ILogger<SaveService> _logger;

    public SaveService(IPathResolver pathResolver, IBackupService backupService, ILogger<SaveService> logger)
    {
        _pathResolver = pathResolver;
        _backupService = backupService;
        _logger = logger;
    }

    public Task CopyLocalSavesToRepositoryAsync(Game game, string repositoryLocalPath, CancellationToken cancellationToken = default) =>
        CopyLocalToRepositoryAsync(game, repositoryLocalPath, cancellationToken);

    public Task RestoreSavesFromRepositoryAsync(Game game, string repositoryLocalPath, CancellationToken cancellationToken = default) =>
        RestoreRepositoryToLocalAsync(game, repositoryLocalPath, createBackup: true, cancellationToken);

    public async Task<bool> HasLocalSaveChangesAsync(Game game, string repositoryLocalPath, CancellationToken cancellationToken = default)
    {
        var changes = await DetectChangesAsync(game, repositoryLocalPath, cancellationToken).ConfigureAwait(false);
        return changes.HasChanges;
    }

    public Task CopyLocalToRepositoryAsync(Game game, string repositoryLocalPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryLocalPath);

        foreach (var location in game.SaveLocations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var local = _pathResolver.Resolve(location.LocalPath);
            if (!_pathResolver.IsAllowedLocalSaveTarget(local))
            {
                throw new ConfigurationValidationException(
                    [$"Save path for '{game.Id}/{location.Id}' is not allowed: '{local}'."]);
            }

            var remote = _pathResolver.MapRemotePathToRepository(repositoryLocalPath, location.RemotePath);

            if (location.Type == SaveLocationType.File)
            {
                if (!File.Exists(local))
                {
                    _logger.LogWarning("Local save file missing for {GameId}/{SaveId}: {Path}", game.Id, location.Id, local);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(remote)!);
                AtomicFile.Copy(local, remote, cancellationToken);
            }
            else
            {
                if (!Directory.Exists(local))
                {
                    _logger.LogWarning("Local save directory missing for {GameId}/{SaveId}: {Path}", game.Id, location.Id, local);
                    continue;
                }

                MirrorDirectory(local, remote, deleteExtraneous: true, cancellationToken);
            }

            _logger.LogInformation("Copied local saves {GameId}/{SaveId} to repository", game.Id, location.Id);
        }

        return Task.CompletedTask;
    }

    public async Task RestoreRepositoryToLocalAsync(Game game, string repositoryLocalPath, bool createBackup = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryLocalPath);

        var existingLocals = new List<string>();
        foreach (var location in game.SaveLocations)
        {
            var local = _pathResolver.Resolve(location.LocalPath);
            if (!_pathResolver.IsAllowedLocalSaveTarget(local))
            {
                throw new ConfigurationValidationException(
                    [$"Save path for '{game.Id}/{location.Id}' is not allowed: '{local}'."]);
            }

            if (File.Exists(local) || Directory.Exists(local))
            {
                existingLocals.Add(local);
            }
        }

        if (createBackup && existingLocals.Count > 0)
        {
            await _backupService.CreateBackupAsync(game.Id, existingLocals, cancellationToken).ConfigureAwait(false);
        }

        foreach (var location in game.SaveLocations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var local = _pathResolver.Resolve(location.LocalPath);
            var remote = _pathResolver.MapRemotePathToRepository(repositoryLocalPath, location.RemotePath);

            if (location.Type == SaveLocationType.File)
            {
                if (!File.Exists(remote))
                {
                    throw new SavePathNotFoundException($"Repository save file not found for '{game.Id}/{location.Id}'.", remote);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(local)!);
                AtomicFile.Copy(remote, local, cancellationToken);
            }
            else
            {
                if (!Directory.Exists(remote))
                {
                    // Empty remote directory is allowed — create empty local if needed.
                    Directory.CreateDirectory(local);
                    continue;
                }

                Directory.CreateDirectory(local);
                MirrorDirectory(remote, local, deleteExtraneous: true, cancellationToken);
            }

            _logger.LogInformation("Restored repository saves {GameId}/{SaveId} to local path", game.Id, location.Id);
        }
    }

    public Task<SaveChangesDetected> DetectChangesAsync(Game game, string repositoryLocalPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryLocalPath);

        var added = new List<string>();
        var changed = new List<string>();
        var deleted = new List<string>();

        foreach (var location in game.SaveLocations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var local = _pathResolver.Resolve(location.LocalPath);
            var remote = _pathResolver.MapRemotePathToRepository(repositoryLocalPath, location.RemotePath);
            var localExists = location.Type == SaveLocationType.File
                ? File.Exists(local)
                : Directory.Exists(local);

            if (!localExists)
            {
                _logger.LogDebug(
                    "Skipping change detection for {GameId}/{SaveId}; local path does not exist: {Path}",
                    game.Id,
                    location.Id,
                    local);
                continue;
            }

            var prefix = $"{location.Id}/";

            var comparison = FileTreeComparer.Compare(local, remote, location.Type);
            added.AddRange(comparison.AddedFiles.Select(f => prefix + f));
            changed.AddRange(comparison.ChangedFiles.Select(f => prefix + f));
            deleted.AddRange(comparison.DeletedFiles.Select(f => prefix + f));
        }

        return Task.FromResult(new SaveChangesDetected
        {
            AddedFiles = added,
            ChangedFiles = changed,
            DeletedFiles = deleted
        });
    }

    public bool HasRepositorySaveContent(Game game, string repositoryLocalPath)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryLocalPath);

        foreach (var location in game.SaveLocations)
        {
            var remote = _pathResolver.MapRemotePathToRepository(repositoryLocalPath, location.RemotePath);
            if (location.Type == SaveLocationType.File)
            {
                if (File.Exists(remote))
                {
                    return true;
                }

                continue;
            }

            if (Directory.Exists(remote) && FileTreeComparer.EnumerateRelativeFiles(remote).Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasLocalSaveContent(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        foreach (var location in game.SaveLocations)
        {
            var local = _pathResolver.Resolve(location.LocalPath);
            if (location.Type == SaveLocationType.File)
            {
                if (File.Exists(local))
                {
                    return true;
                }

                continue;
            }

            if (Directory.Exists(local) && FileTreeComparer.EnumerateRelativeFiles(local).Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void MirrorDirectory(string sourceDir, string destDir, bool deleteExtraneous, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destDir);

        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(destDir, relative));
        }

        var sourceFiles = FileTreeComparer.EnumerateRelativeFiles(sourceDir);
        foreach (var relative in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceFile = Path.Combine(sourceDir, relative.Replace('/', Path.DirectorySeparatorChar));
            var destFile = Path.Combine(destDir, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            AtomicFile.Copy(sourceFile, destFile, cancellationToken);
        }

        if (!deleteExtraneous)
        {
            return;
        }

        var destFiles = FileTreeComparer.EnumerateRelativeFiles(destDir);
        foreach (var relative in destFiles)
        {
            if (sourceFiles.Contains(relative))
            {
                continue;
            }

            var destFile = Path.Combine(destDir, relative.Replace('/', Path.DirectorySeparatorChar));
            File.Delete(destFile);
        }

        // Remove empty directories left behind after deletions.
        foreach (var directory in Directory.EnumerateDirectories(destDir, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }
}
