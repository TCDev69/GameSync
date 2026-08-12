namespace GameSync.Core.GitHub;

/// <summary>
/// Shared credential keys for GitHub secrets in Windows Credential Manager.
/// Values are stored under the GameSync/ prefix by <c>WindowsCredentialStore</c>.
/// </summary>
public static class GitHubCredentialKeys
{
    public const string AccessToken = "GitHub/AccessToken";
}
