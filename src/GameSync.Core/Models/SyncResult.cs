namespace GameSync.Core.Models;

/// <summary>
/// Outcome of a sync or launch-workflow operation.
/// </summary>
public sealed class SyncResult
{
    public required bool Succeeded { get; init; }

    public SyncStatus Status { get; init; }

    public string? Message { get; init; }

    public string? GameId { get; init; }

    public IReadOnlyList<Conflict> Conflicts { get; init; } = Array.Empty<Conflict>();

    public Exception? Error { get; init; }

    public static SyncResult Success(SyncStatus status, string? message = null, string? gameId = null) =>
        new()
        {
            Succeeded = true,
            Status = status,
            Message = message,
            GameId = gameId
        };

    public static SyncResult Failure(SyncStatus status, string message, string? gameId = null, Exception? error = null, IReadOnlyList<Conflict>? conflicts = null) =>
        new()
        {
            Succeeded = false,
            Status = status,
            Message = message,
            GameId = gameId,
            Error = error,
            Conflicts = conflicts ?? Array.Empty<Conflict>()
        };
}
