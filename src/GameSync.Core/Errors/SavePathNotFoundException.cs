namespace GameSync.Core.Errors;

public sealed class SavePathNotFoundException : GameSyncException
{
    public string? Path { get; }

    public SavePathNotFoundException(string message, string? path = null)
        : base("SavePathNotFound", message)
    {
        Path = path;
    }
}
