using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Sync;

/// <summary>
/// Compatibility surface used by earlier foundation code. Prefer <see cref="ISaveService"/>.
/// </summary>
public interface ISaveSyncService
{
    Task CopyLocalSavesToRepositoryAsync(Game game, string repositoryLocalPath, CancellationToken cancellationToken = default);

    Task RestoreSavesFromRepositoryAsync(Game game, string repositoryLocalPath, CancellationToken cancellationToken = default);

    Task<bool> HasLocalSaveChangesAsync(Game game, string repositoryLocalPath, CancellationToken cancellationToken = default);
}
