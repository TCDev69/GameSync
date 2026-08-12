namespace GameSync.Core.Models;

/// <summary>
/// Normalized metadata returned by an <c>IGameMetadataProvider</c>.
/// </summary>
public sealed class GameMetadata
{
    public required string ExternalId { get; init; }

    public required string ProviderId { get; init; }

    public required string Title { get; init; }

    public string? CoverUrl { get; init; }

    public DateOnly? ReleaseDate { get; init; }

    public string? Platform { get; init; }

    public string? Summary { get; init; }
}
