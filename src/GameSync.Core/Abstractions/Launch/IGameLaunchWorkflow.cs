using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Launch;

/// <summary>
/// Orchestrates the full pre-launch sync → launch → post-exit sync workflow.
/// Must not be implemented inside UI code-behind.
/// </summary>
public interface IGameLaunchWorkflow
{
    Task<SyncResult> ExecuteAsync(string gameId, CancellationToken cancellationToken = default);
}
