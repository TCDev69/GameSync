using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Configuration;

public interface IUiSettingsStore
{
    Task<UiSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UiSettings settings, CancellationToken cancellationToken = default);
}
