namespace GameSync.Core.Errors;

public sealed class GitHubUnavailableException : GameSyncException
{
    public GitHubUnavailableException(string message)
        : base("GitHubUnavailable", message)
    {
    }

    public GitHubUnavailableException(string message, Exception innerException)
        : base("GitHubUnavailable", message, innerException)
    {
    }
}
