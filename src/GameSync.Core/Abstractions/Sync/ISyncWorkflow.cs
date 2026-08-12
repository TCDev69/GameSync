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

    /// <summary>
    /// Resolves sync status for every configured game with a single Git repository read.
    /// Intended for library/history views that would otherwise call <see cref="GetStatusAsync"/>
    /// once per game.
    /// </summary>
    Task<IReadOnlyDictionary<string, SyncStatus>> GetGameStatusesAsync(CancellationToken cancellationToken = default);
}
