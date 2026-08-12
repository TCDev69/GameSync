namespace GameSync.Core.Errors;

public sealed class GitHubAuthenticationFailedException : GameSyncException
{
    public GitHubAuthenticationFailedException(string message)
        : base("GitHubAuthenticationFailed", message)
    {
    }

    public GitHubAuthenticationFailedException(string message, Exception innerException)
        : base("GitHubAuthenticationFailed", message, innerException)
    {
    }
}
