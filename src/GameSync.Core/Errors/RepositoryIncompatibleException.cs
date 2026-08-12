namespace GameSync.Core.Errors;

public sealed class RepositoryIncompatibleException : GameSyncException
{
    public RepositoryIncompatibleException(string message)
        : base("RepositoryIncompatible", message)
    {
    }

    public RepositoryIncompatibleException(string message, Exception innerException)
        : base("RepositoryIncompatible", message, innerException)
    {
    }
}
