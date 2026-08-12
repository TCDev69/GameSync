using GameSync.Core.Abstractions.Launch;

namespace GameSync.App.Cli;

public sealed class ConsoleGameSessionAwaiter : IGameSessionAwaiter
{
    public Task WaitForSessionEndAsync(string gameTitle, CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine($"Launched {gameTitle} via Steam/protocol.");
        Console.WriteLine("Press Enter when you have finished playing...");
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.ReadLine();
        }, cancellationToken);
    }

    public void CompleteSession()
    {
    }
}
