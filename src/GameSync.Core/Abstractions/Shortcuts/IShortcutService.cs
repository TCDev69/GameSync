using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Shortcuts;

public interface IShortcutService
{
    Task CreateAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default);

    Task DeleteAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default);

    string GetShortcutPath(ShortcutConfiguration configuration);

    /// <summary>
    /// Arguments that must be used on shortcuts so they always go through GameSync.
    /// </summary>
    string BuildLaunchArguments(string gameId);
}
