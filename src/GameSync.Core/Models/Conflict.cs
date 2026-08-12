namespace GameSync.Core.Models;

/// <summary>
/// Represents a Git conflict for a save path. Binary saves are not mergeable.
/// </summary>
public sealed class Conflict
{
    public required string Path { get; init; }

    public ConflictType Type { get; init; } = ConflictType.Content;

    public string? GameId { get; init; }

    public string? SaveLocationId { get; init; }

    public string? LocalPath { get; init; }

    public string? RemotePath { get; init; }

    public string? Message { get; init; }

    public bool IsBinary { get; init; } = true;
}
