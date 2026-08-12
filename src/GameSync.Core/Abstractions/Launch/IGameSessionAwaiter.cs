namespace GameSync.Core.Abstractions.Launch;

/// <summary>
/// Blocks until the user indicates a game session has ended (protocol launch without a monitor process).
/// </summary>
public interface IGameSessionAwaiter
{
    Task WaitForSessionEndAsync(string gameTitle, CancellationToken cancellationToken = default);

    void CompleteSession();
}
