namespace GameSync.Core.Models;

/// <summary>
/// Lightweight search hit used when adding a game.
/// </summary>
public sealed class GameSearchResult
{
    public required string ExternalId { get; init; }

    public required string ProviderId { get; init; }

    public required string Title { get; init; }

    public string? CoverUrl { get; init; }

    public DateOnly? ReleaseDate { get; init; }

    public string? Platform { get; init; }
}
