namespace GameSync.Core.Errors;

public sealed class RepositoryUnavailableException : GameSyncException
{
    public RepositoryUnavailableException(string message)
        : base("RepositoryUnavailable", message)
    {
    }

    public RepositoryUnavailableException(string message, Exception innerException)
        : base("RepositoryUnavailable", message, innerException)
    {
    }
}
