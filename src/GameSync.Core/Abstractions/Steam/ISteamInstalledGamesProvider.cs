using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Steam;

public interface ISteamInstalledGamesProvider
{
    Task<IReadOnlyList<SteamInstalledGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default);
}
