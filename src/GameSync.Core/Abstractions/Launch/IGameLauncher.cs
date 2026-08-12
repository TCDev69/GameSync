using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Launch;

/// <summary>
/// Full GameSync launch lifecycle: validate → sync before → start → wait → sync after.
/// </summary>
public interface IGameLauncher
{
    Task<GameLaunchResult> LaunchAsync(
        string gameId,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
