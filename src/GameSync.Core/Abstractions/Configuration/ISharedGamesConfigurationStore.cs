using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Configuration;

/// <summary>
/// Reads and writes shared games.json from the cloned repository.
/// </summary>
public interface ISharedGamesConfigurationStore
{
    Task<GamesConfiguration> LoadAsync(string repositoryLocalPath, CancellationToken cancellationToken = default);

    Task SaveAsync(string repositoryLocalPath, GamesConfiguration configuration, CancellationToken cancellationToken = default);

    string GetConfigurationRelativePath();
}
