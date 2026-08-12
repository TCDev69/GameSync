using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Repository;

/// <summary>
/// Manages the deterministic local clone under %LOCALAPPDATA%\GameSync\repositories\.
/// </summary>
public interface IRepositoryService
{
    /// <summary>
    /// Returns the deterministic local path for an owner/name pair without cloning.
    /// </summary>
    string GetLocalRepositoryPath(string owner, string name);

    /// <summary>
    /// Ensures a local clone exists for the configured repository. Does not re-clone if already present.
    /// </summary>
    Task<RepositoryConfiguration> EnsureLocalRepositoryAsync(RepositoryConfiguration configuration, CancellationToken cancellationToken = default);

    Task<bool> IsLocalRepositoryReadyAsync(string localPath, CancellationToken cancellationToken = default);

    string? BuildCloneUrl(RepositoryConfiguration configuration);
}
