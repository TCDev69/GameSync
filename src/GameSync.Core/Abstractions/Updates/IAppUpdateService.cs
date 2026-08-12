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
    /// Starts a safe update install (downloads Setup.exe from GitHub Releases). Does not delete user data.
    /// </summary>
    Task UpdateAsync(CancellationToken cancellationToken = default);
}
