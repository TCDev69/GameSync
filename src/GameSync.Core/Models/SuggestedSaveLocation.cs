namespace GameSync.Core.Models;

/// <summary>
/// Suggested save location from an <c>ISaveLocationProvider</c>.
/// </summary>
public sealed class SuggestedSaveLocation
{
    public required string DisplayName { get; init; }

    public required SaveLocationType Type { get; init; }

    public required string LocalPathTemplate { get; init; }

    public string? Notes { get; init; }
}
