namespace GameSync.Core.Errors;

public sealed class GitPullFailedException : GameSyncException
{
    public GitPullFailedException(string message)
        : base("GitPullFailed", message)
    {
    }

    public GitPullFailedException(string message, Exception innerException)
        : base("GitPullFailed", message, innerException)
    {
    }
}
