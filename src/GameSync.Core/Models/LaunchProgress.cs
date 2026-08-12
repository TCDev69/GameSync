namespace GameSync.Core.Models;

public sealed class LaunchProgress
{
    public required LaunchPhase Phase { get; init; }

    public required string Message { get; init; }

    public string? GameId { get; init; }

    public string? GameTitle { get; init; }

    public int? ProcessId { get; init; }

    public SyncResult? LastSyncResult { get; init; }

    public Exception? Error { get; init; }
}
