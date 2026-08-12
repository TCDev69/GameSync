using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.GitHub;

/// <summary>
/// Low-level GitHub REST API operations using a caller-supplied access token (mockable).
/// The token must never be logged by implementations.
/// </summary>
public interface IGitHubApiClient
{
    Task<GitHubUser> GetAuthenticatedUserAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RepositoryConfiguration>> GetRepositoriesAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<RepositoryConfiguration> GetRepositoryAsync(string accessToken, string owner, string name, CancellationToken cancellationToken = default);
}
