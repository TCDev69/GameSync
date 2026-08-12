namespace GameSync.Core.Models;

/// <summary>
/// A historical save commit entry for browsing and restore.
/// </summary>
public sealed class SaveHistoryEntry
{
    public required string CommitSha { get; init; }

    public required DateTimeOffset CommittedAt { get; init; }

    public required string Message { get; init; }

    public string? AuthorName { get; init; }

    public string? GameId { get; init; }

    public IReadOnlyList<string> ChangedPaths { get; init; } = Array.Empty<string>();
}
