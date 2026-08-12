using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Sync;

/// <summary>
/// High-level save synchronization around game launch and exit.
/// </summary>
public interface ISyncService
{
    Task<SyncResult> SyncBeforeGameLaunchAsync(string gameId, CancellationToken cancellationToken = default);

    Task<SyncResult> SyncAfterGameExitAsync(string gameId, CancellationToken cancellationToken = default);
}
