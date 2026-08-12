namespace GameSync.Core.Models;

/// <summary>
/// Local backup retention policy under %LOCALAPPDATA%\GameSync\backups\.
/// </summary>
public sealed class BackupSettings
{
    /// <summary>
    /// Maximum number of timestamped backups retained per game. Zero disables pruning.
    /// </summary>
    public int MaxBackupsPerGame { get; init; } = 10;

    public bool Enabled { get; init; } = true;
}
