using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Metadata;

public interface IGameMetadataProvider
{
    string ProviderId { get; }

    Task<IReadOnlyList<GameSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<GameMetadata?> GetMetadataAsync(string externalId, CancellationToken cancellationToken = default);
}
