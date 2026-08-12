namespace GameSync.Core.Models;

/// <summary>
/// Outcome of a self-update install started by <see cref="Abstractions.Updates.IAppUpdateService"/>.
/// </summary>
public sealed class AppUpdateInstallResult
{
    public required bool InstallerStarted { get; init; }

    public required string Version { get; init; }

    /// <summary>
    /// Local path of the verified installer that was launched.
    /// </summary>
    public string? InstallerPath { get; init; }

    public int? ProcessId { get; init; }

    /// <summary>
    /// True when the caller must shut the app down so the installer can replace program files.
    /// </summary>
    public bool ShouldExitApplication { get; init; }

    public string? Message { get; init; }
}
