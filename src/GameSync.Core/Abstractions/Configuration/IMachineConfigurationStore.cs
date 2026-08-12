using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Configuration;

public interface IMachineConfigurationStore
{
    Task<MachineConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(MachineConfiguration configuration, CancellationToken cancellationToken = default);
}
