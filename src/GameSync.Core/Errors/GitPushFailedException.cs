namespace GameSync.Core.Errors;

public sealed class GitPushFailedException : GameSyncException
{
    public GitPushFailedException(string message)
        : base("GitPushFailed", message)
    {
    }

    public GitPushFailedException(string message, Exception innerException)
        : base("GitPushFailed", message, innerException)
    {
    }
}
