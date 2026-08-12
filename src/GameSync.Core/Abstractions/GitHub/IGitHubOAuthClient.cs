using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.GitHub;

/// <summary>
/// Low-level GitHub OAuth device-flow HTTP operations (mockable).
/// </summary>
public interface IGitHubOAuthClient
{
    Task<GitHubDeviceAuthorization> RequestDeviceCodeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls once for an access token. Returns null when still pending.
    /// </summary>
    Task<string?> TryGetAccessTokenAsync(string deviceCode, CancellationToken cancellationToken = default);
}
