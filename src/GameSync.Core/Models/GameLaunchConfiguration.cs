namespace GameSync.Core.Models;

/// <summary>
/// Machine-local launch settings for a single game.
/// </summary>
public sealed class GameLaunchConfiguration
{
    /// <summary>
    /// Local .exe path or a protocol URI (e.g. steam://run/1091500).
    /// </summary>
    public required string Executable { get; init; }

    public string Arguments { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Optional process to watch after a protocol launch (e.g. the game .exe while using steam://).
    /// </summary>
    public string MonitorExecutable { get; init; } = string.Empty;
}
