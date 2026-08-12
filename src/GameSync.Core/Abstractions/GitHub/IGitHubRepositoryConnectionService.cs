using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.GitHub;

/// <summary>
/// Connects a user-selected GitHub repository as the GameSync save backend.
/// </summary>
public interface IGitHubRepositoryConnectionService
{
    Task<RepositoryConnectionResult> ConnectRepositoryAsync(
        RepositoryConfiguration selectedRepository,
        CancellationToken cancellationToken = default);
}
