namespace GameSync.Core.Errors;

public sealed class PathTraversalException : GameSyncException
{
    public string? Path { get; }

    public PathTraversalException(string message, string? path = null)
        : base("PathTraversal", message)
    {
        Path = path;
    }
}
