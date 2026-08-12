using GameSync.Core.Abstractions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameSync.Core.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddGameSyncCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IConfigurationValidator, Configuration.ConfigurationValidator>();
        return services;
    }
}
