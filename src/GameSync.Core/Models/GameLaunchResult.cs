namespace GameSync.Core.Models;

public sealed class GameLaunchResult
{
    public required bool Succeeded { get; init; }

    public required string GameId { get; init; }

    public string? GameTitle { get; init; }

    public int? ProcessId { get; init; }

    public int? ExitCode { get; init; }

    public SyncResult? PreLaunchSync { get; init; }

    public SyncResult? PostExitSync { get; init; }

    public string? Message { get; init; }

    public Exception? Error { get; init; }

    public bool WasCancelled { get; init; }
}
