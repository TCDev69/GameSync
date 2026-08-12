namespace GameSync.Core.Models;

public sealed class AppUpdateCheckResult
{
    public required bool UpdateAvailable { get; init; }

    public required string CurrentVersion { get; init; }

    public string? LatestVersion { get; init; }

    public string? ReleaseNotesUrl { get; init; }

    public string? InstallerUri { get; init; }

    public string? PackageDownloadUri { get; init; }

    /// <summary>
    /// Installer asset file name as published on the release.
    /// </summary>
    public string? InstallerFileName { get; init; }

    /// <summary>
    /// Expected installer size in bytes, when the release reports it.
    /// </summary>
    public long? InstallerSizeBytes { get; init; }

    /// <summary>
    /// Lowercase hex SHA-256 of the installer, when the release publishes a digest.
    /// The installer is not code-signed, so this is the integrity check for the download.
    /// </summary>
    public string? InstallerSha256 { get; init; }

    public string? Message { get; init; }

    public static AppUpdateCheckResult None(string current, string? message = null) => new()
    {
        UpdateAvailable = false,
        CurrentVersion = current,
        Message = message
    };
}
