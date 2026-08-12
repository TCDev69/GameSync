namespace GameSync.Core.Models;

public sealed class AppUpdateCheckResult
{
    public required bool UpdateAvailable { get; init; }

    public required string CurrentVersion { get; init; }

    public string? LatestVersion { get; init; }

    public string? ReleaseNotesUrl { get; init; }

    public string? InstallerUri { get; init; }

    public string? PackageDownloadUri { get; init; }

    public string? Message { get; init; }

    public static AppUpdateCheckResult None(string current, string? message = null) => new()
    {
        UpdateAvailable = false,
        CurrentVersion = current,
        Message = message
    };
}
