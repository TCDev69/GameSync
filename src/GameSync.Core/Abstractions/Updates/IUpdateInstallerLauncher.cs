namespace GameSync.Core.Abstractions.Updates;

/// <summary>
/// Starts a downloaded installer through the OS shell so Windows can show the elevation prompt.
/// Separate from IProcessLauncher, which starts games without shell execution and cannot elevate.
/// </summary>
public interface IUpdateInstallerLauncher
{
    /// <summary>
    /// Returns the process id of the started installer.
    /// </summary>
    Task<int> StartAsync(string installerPath, string arguments, CancellationToken cancellationToken = default);
}
