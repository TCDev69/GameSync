using GameSync.Core.Abstractions.Git;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Errors;
using GameSync.Core.Models;
using GameSync.Infrastructure.IO;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using DomainConflict = GameSync.Core.Models.Conflict;
using LibGitRepository = LibGit2Sharp.Repository;

namespace GameSync.Infrastructure.Sync;

/// <summary>
/// Applies Local / Remote / Abort resolutions. Never silently merges binary saves.
/// </summary>
public sealed class ConflictResolver : IConflictResolver
{
    private readonly IBackupService _backupService;
    private readonly ILogger<ConflictResolver> _logger;

    public ConflictResolver(IBackupService backupService, ILogger<ConflictResolver> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    public ConflictResolution ToResolution(ConflictResolutionChoice choice) =>
        choice switch
        {
            ConflictResolutionChoice.UseLocal => ConflictResolution.Local,
            ConflictResolutionChoice.UseRemote => ConflictResolution.Remote,
            ConflictResolutionChoice.Cancel => ConflictResolution.Abort,
            ConflictResolutionChoice.ViewHistory => ConflictResolution.Abort,
            _ => ConflictResolution.Abort
        };

    public async Task ApplyAsync(string repositoryLocalPath, DomainConflict conflict, ConflictResolution resolution, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryLocalPath);
        ArgumentNullException.ThrowIfNull(conflict);
        cancellationToken.ThrowIfCancellationRequested();

        if (resolution == ConflictResolution.Abort)
        {
            _logger.LogInformation("Conflict resolution aborted for {Path}", conflict.Path);
            return;
        }

        if (!LibGitRepository.IsValid(repositoryLocalPath))
        {
            throw new RepositoryUnavailableException($"No valid Git repository at '{repositoryLocalPath}'.");
        }

        using var repo = new LibGitRepository(repositoryLocalPath);
        var absolute = Path.Combine(repo.Info.WorkingDirectory, conflict.Path.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(absolute))
        {
            var gameId = conflict.GameId ?? "conflict";
            await _backupService.CreateBackupAsync(gameId, [absolute], cancellationToken).ConfigureAwait(false);
        }

        var entry = repo.Index.Conflicts[conflict.Path]
            ?? throw new GitConflictDetectedException(
                $"No conflict entry found for '{conflict.Path}'.",
                [conflict]);

        IndexEntry? chosen = resolution == ConflictResolution.Local ? entry.Ours : entry.Theirs;
        if (chosen is null)
        {
            if (File.Exists(absolute))
            {
                File.Delete(absolute);
            }

            repo.Index.Remove(conflict.Path);
        }
        else
        {
            var blob = repo.Lookup<Blob>(chosen.Id);
            WriteBlob(absolute, blob, cancellationToken);
            repo.Index.Add(conflict.Path);
        }

        repo.Index.Write();
        _logger.LogInformation("Applied {Resolution} for conflict {Path}", resolution, conflict.Path);
    }

    private static void WriteBlob(string absolute, Blob? blob, CancellationToken cancellationToken)
    {
        if (blob is null)
        {
            return;
        }

        using var stream = blob.GetContentStream();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        AtomicFile.WriteAllBytes(absolute, ms.ToArray(), cancellationToken);
    }
}
