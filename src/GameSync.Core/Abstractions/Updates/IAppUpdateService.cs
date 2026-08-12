using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Updates;

/// <summary>
/// Application self-update via GitHub Releases installer download.
/// </summary>
public interface IAppUpdateService
{
    Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<bool> IsUpdateAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the release installer, verifies its integrity and starts it unattended.
    /// User data under %LOCALAPPDATA%\GameSync\ lives outside the install directory and is never touched.
    /// </summary>
    /// <param name="progress">Reports download completion from 0 to 100.</param>
    Task<AppUpdateInstallResult> UpdateAsync(
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
