using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.GitHub;

public interface IGitHubAuthenticationService
{
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts device authorization and returns the user-visible code + verification URL.
    /// </summary>
    Task<GitHubDeviceAuthorization> StartAuthenticationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the GitHub verification URL in the user's default browser.
    /// </summary>
    Task OpenAuthenticationUrlAsync(GitHubDeviceAuthorization authorization, CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls GitHub until the user completes authorization, then stores the access token securely.
    /// </summary>
    Task CompleteAuthenticationAsync(GitHubDeviceAuthorization authorization, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience: start → open browser → poll → store token.
    /// </summary>
    Task AuthenticateAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task<GitHubUser> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default);
}
