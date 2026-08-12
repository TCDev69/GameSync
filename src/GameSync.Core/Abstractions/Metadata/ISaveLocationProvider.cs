using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Metadata;

public interface ISaveLocationProvider
{
    string ProviderId { get; }

    Task<IReadOnlyList<SuggestedSaveLocation>> SuggestAsync(GameMetadata metadata, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SuggestedSaveLocation>> SuggestByGameIdAsync(string gameId, string title, CancellationToken cancellationToken = default);
}
