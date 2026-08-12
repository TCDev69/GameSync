using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.GitHub;

public interface IGitHubService
{
    Task<GitHubUser> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RepositoryConfiguration>> GetRepositoriesAsync(CancellationToken cancellationToken = default);

    Task<RepositoryConfiguration> GetRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default);

    Task VerifyRepositoryAccessAsync(string owner, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compatibility alias for <see cref="GetRepositoriesAsync"/>.
    /// </summary>
    Task<IReadOnlyList<RepositoryConfiguration>> ListRepositoriesAsync(CancellationToken cancellationToken = default) =>
        GetRepositoriesAsync(cancellationToken);

    /// <summary>
    /// Compatibility alias for <see cref="VerifyRepositoryAccessAsync"/>.
    /// </summary>
    Task VerifyAccessAsync(string owner, string name, CancellationToken cancellationToken = default) =>
        VerifyRepositoryAccessAsync(owner, name, cancellationToken);
}
