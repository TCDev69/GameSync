namespace GameSync.Core.Models;

/// <summary>
/// Root document for shared repository configuration (config/games.json).
/// </summary>
public sealed class GamesConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public IList<Game> Games { get; init; } = new List<Game>();
}
