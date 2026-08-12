using GameSync.Core.Models;

namespace GameSync.Core.Abstractions.Configuration;

public interface IConfigurationValidator
{
    IReadOnlyList<string> Validate(GamesConfiguration configuration);

    IReadOnlyList<string> Validate(MachineConfiguration configuration);
}
