namespace GameSync.Core.Models;

/// <summary>
/// Local-only machine configuration stored under %LOCALAPPDATA%\GameSync\machine.json.
/// Never synchronized through Git.
/// </summary>
public sealed class MachineConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// Friendly machine identifier (defaults to Environment.MachineName).
    /// </summary>
    public required string MachineId { get; init; }

    /// <summary>
    /// Optional selected repository clone path and remote metadata for this machine.
    /// </summary>
    public RepositoryConfiguration? Repository { get; init; }

    /// <summary>
    /// Per-game launch overrides keyed by shared game id.
    /// </summary>
    public Dictionary<string, GameLaunchConfiguration> Games { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public BackupSettings Backup { get; init; } = new();
}
