namespace GameSync.Core.Models;

/// <summary>
/// Maps a local save path to a remote path inside the user's GitHub repository.
/// Local paths may contain Windows environment variables (e.g. %USERPROFILE%).
/// </summary>
public sealed class SaveLocation
{
    public required string Id { get; init; }

    public SaveLocationType Type { get; init; } = SaveLocationType.Directory;

    /// <summary>
    /// Path relative to the repository root, e.g. saves/cyberpunk_2077/main.
    /// </summary>
    public required string RemotePath { get; init; }

    /// <summary>
    /// Local path template with optional environment variables.
    /// </summary>
    public required string LocalPath { get; init; }

    public string? DisplayName { get; init; }
}
