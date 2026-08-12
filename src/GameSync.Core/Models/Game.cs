namespace GameSync.Core.Models;

/// <summary>
/// Shared game definition stored in the repository games.json.
/// Machine-specific launch paths live in local machine.json, not here.
/// </summary>
public sealed class Game
{
    /// <summary>
    /// Stable identifier used across machines and CLI (e.g. cyberpunk_2077).
    /// </summary>
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string? CoverUrl { get; init; }

    public string? MetadataProviderId { get; init; }

    public string? MetadataExternalId { get; init; }

    public DateOnly? ReleaseDate { get; init; }

    public string? Platform { get; init; }

    public IList<SaveLocation> SaveLocations { get; init; } = new List<SaveLocation>();
}
