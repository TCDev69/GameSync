using GameSync.Core.Models;

namespace GameSync.Core.Services;

/// <summary>
/// Maps shared game definitions to repository save paths and validates mapping consistency.
/// </summary>
public static class SaveMapping
{
    public static string BuildDefaultRemotePath(string gameId, string saveLocationId) =>
        Path.Combine("saves", gameId, saveLocationId).Replace('\\', '/');

    public static string SuggestGameId(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var chars = title
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();

        var collapsed = new string(chars);
        while (collapsed.Contains("__", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("__", "_", StringComparison.Ordinal);
        }

        return collapsed.Trim('_');
    }

    public static IReadOnlyDictionary<string, SaveLocation> IndexById(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.SaveLocations.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
    }
}
