namespace GameSync.Core.Options;

/// <summary>
/// Application-level options that are not machine-specific game launch data.
/// </summary>
public sealed class GameSyncOptions
{
    public const string SectionName = "GameSync";

    /// <summary>
    /// Official GitHub owner for Releases-based self-updates.
    /// </summary>
    public const string DefaultUpdateReleasesOwner = "TCDev69";

    public const string DefaultUpdateReleasesRepo = "GameSync";

    /// <summary>
    /// Credential Manager target name prefix for GitHub tokens.
    /// </summary>
    public string CredentialTargetPrefix { get; set; } = "GameSync/GitHub";

    public string DefaultBranch { get; set; } = "main";

    /// <summary>
    /// Public OAuth App client ID used for the GitHub device authorization flow.
    /// Not a secret. Override with environment variable GAMESYNC_GITHUB_CLIENT_ID if needed.
    /// Never commit a client secret (device flow uses client_id only).
    /// </summary>
    public string GitHubClientId { get; set; } = "Ov23lifRWe1kBZkxMufT";

    /// <summary>
    /// OAuth scopes requested by device flow. Keep minimal.
    /// </summary>
    public string GitHubScopes { get; set; } = "read:user repo";

    public string GitHubApiBaseUrl { get; set; } = "https://api.github.com/";

    public string GitHubOAuthBaseUrl { get; set; } = "https://github.com/";

    /// <summary>
    /// GitHub owner for Releases-based updates.
    /// Override with GAMESYNC_UPDATE_OWNER.
    /// </summary>
    public string UpdateReleasesOwner { get; set; } = DefaultUpdateReleasesOwner;

    /// <summary>
    /// GitHub repository name for Releases-based updates.
    /// Override with GAMESYNC_UPDATE_REPO.
    /// </summary>
    public string UpdateReleasesRepo { get; set; } = DefaultUpdateReleasesRepo;

    /// <summary>
    /// v1.0.0 builds and stale MSIX debug output used <c>TCDev</c> instead of <c>TCDev69</c>.
    /// </summary>
    public void NormalizeUpdateFeed()
    {
        if (string.Equals(UpdateReleasesOwner, "TCDev", StringComparison.OrdinalIgnoreCase)
            && string.Equals(UpdateReleasesRepo, DefaultUpdateReleasesRepo, StringComparison.OrdinalIgnoreCase))
        {
            UpdateReleasesOwner = DefaultUpdateReleasesOwner;
        }
    }

    /// <summary>
    /// Checks GitHub Releases in the background shortly after the window opens.
    /// Nothing is downloaded or installed without an explicit confirmation.
    /// </summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;
}
