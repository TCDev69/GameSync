using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Sync;

/// <summary>
/// Legacy/general sync workflow surface. Prefer <see cref="ISyncService"/> for launch/exit.
/// </summary>
public interface ISyncWorkflow
{
    Task<SyncResult> SyncAllAsync(CancellationToken cancellationToken = default);

    Task<SyncResult> SyncGameAsync(string gameId, CancellationToken cancellationToken = default);

    Task<SyncStatus> GetStatusAsync(string? gameId = null, CancellationToken cancellationToken = default);
}
