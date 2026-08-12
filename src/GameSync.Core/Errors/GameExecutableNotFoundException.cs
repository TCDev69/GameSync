namespace GameSync.Core.Errors;

public sealed class GameExecutableNotFoundException : GameSyncException
{
    public string? GameId { get; }

    public string? ExecutablePath { get; }

    public GameExecutableNotFoundException(string message, string? executablePath = null, string? gameId = null)
        : base("GameExecutableNotFound", message)
    {
        ExecutablePath = executablePath;
        GameId = gameId;
    }

    public static GameExecutableNotFoundException ForGame(string gameId, string executablePath) =>
        new(
            $"Executable for game '{gameId}' was not found at '{executablePath}'.",
            executablePath,
            gameId);
}
