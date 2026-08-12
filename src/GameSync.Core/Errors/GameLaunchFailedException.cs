namespace GameSync.Core.Errors;

public sealed class GameLaunchFailedException : GameSyncException
{
    public GameLaunchFailedException(string message)
        : base("GameLaunchFailed", message)
    {
    }

    public GameLaunchFailedException(string message, Exception innerException)
        : base("GameLaunchFailed", message, innerException)
    {
    }
}
