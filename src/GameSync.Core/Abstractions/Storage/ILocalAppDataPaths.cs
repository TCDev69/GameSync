namespace GameSync.Core.Abstractions.Storage;

/// <summary>
/// Well-known local application data paths under %LOCALAPPDATA%\GameSync\.
/// </summary>
public interface ILocalAppDataPaths
{
    string Root { get; }

    string MachineConfigurationFile { get; }

    string RepositoriesDirectory { get; }

    string CacheDirectory { get; }

    string LogsDirectory { get; }

    string BackupsDirectory { get; }

    /// <summary>
    /// Holds installers downloaded by the self-update flow.
    /// </summary>
    string UpdatesDirectory { get; }

    /// <summary>
    /// Ensures all required directories exist.
    /// </summary>
    void EnsureCreated();
}
