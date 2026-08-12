using GameSync.Core.Abstractions.Metadata;
using GameSync.Core.Models;
using GameSync.Core.Services;

namespace GameSync.Infrastructure.Metadata;

/// <summary>
/// Suggests common Windows save path templates. Users can always edit suggestions.
/// </summary>
public sealed class HeuristicSaveLocationProvider : ISaveLocationProvider
{
    private static readonly IReadOnlyDictionary<string, string[]> KnownPaths =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["cyberpunk_2077"] =
            [
                "%USERPROFILE%/Saved Games/CD Projekt Red/Cyberpunk 2077"
            ],
            ["minecraft"] =
            [
                "%APPDATA%/.minecraft/saves"
            ],
            ["the_witcher_3"] =
            [
                "%USERPROFILE%/Documents/The Witcher 3/gamesaves"
            ],
            ["elden_ring"] =
            [
                "%APPDATA%/EldenRing"
            ]
        };

    public string ProviderId => "heuristic";

    public Task<IReadOnlyList<SuggestedSaveLocation>> SuggestAsync(GameMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var gameId = SaveMapping.SuggestGameId(metadata.Title);
        return SuggestByGameIdAsync(gameId, metadata.Title, cancellationToken);
    }

    public Task<IReadOnlyList<SuggestedSaveLocation>> SuggestByGameIdAsync(string gameId, string title, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var list = new List<SuggestedSaveLocation>();

        if (!string.IsNullOrWhiteSpace(gameId) && KnownPaths.TryGetValue(gameId, out var known))
        {
            foreach (var path in known)
            {
                list.Add(new SuggestedSaveLocation
                {
                    DisplayName = "Suggested save folder",
                    Type = SaveLocationType.Directory,
                    LocalPathTemplate = path,
                    Notes = "Known path for this game. Edit if your install differs."
                });
            }
        }

        // Always offer generic editable suggestions.
        list.Add(new SuggestedSaveLocation
        {
            DisplayName = "Saved Games",
            Type = SaveLocationType.Directory,
            LocalPathTemplate = $"%USERPROFILE%/Saved Games/{title}",
            Notes = "Generic Windows Saved Games location."
        });
        list.Add(new SuggestedSaveLocation
        {
            DisplayName = "AppData Local",
            Type = SaveLocationType.Directory,
            LocalPathTemplate = $"%LOCALAPPDATA%/{title}",
            Notes = "Common for Unity/Unreal titles."
        });

        return Task.FromResult<IReadOnlyList<SuggestedSaveLocation>>(list);
    }
}
