using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Sync;

/// <summary>
/// Copies and compares game save locations against the local Git repository working tree.
/// </summary>
public interface ISaveService
{
    Task CopyLocalToRepositoryAsync(Game game, string repositoryLocalPath, CancellationToken cancellationToken = default);

    Task RestoreRepositoryToLocalAsync(Game game, string repositoryLocalPath, bool createBackup = true, CancellationToken cancellationToken = default);

    Task<SaveChangesDetected> DetectChangesAsync(Game game, string repositoryLocalPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the repository already contains at least one save file for this game.
    /// </summary>
    bool HasRepositorySaveContent(Game game, string repositoryLocalPath);

    /// <summary>
    /// True when this PC already has at least one local save file for this game.
    /// </summary>
    bool HasLocalSaveContent(Game game);
}
