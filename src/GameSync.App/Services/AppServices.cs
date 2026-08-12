using GameSync.App.Cli;
using GameSync.App.Navigation;
using GameSync.App.Services;
using GameSync.App.ViewModels;
using GameSync.Core.Abstractions.Launch;
using GameSync.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameSync.App.Services;

public static class AppServices
{
    public static IServiceProvider Configure() =>
        Build(services =>
        {
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<AppActivationService>();
            services.AddSingleton<AppActivityService>();
            services.AddSingleton<TaskbarProgressService>();
            services.AddSingleton<UiGameSessionAwaiter>();
            services.AddSingleton<IGameSessionAwaiter>(sp => sp.GetRequiredService<UiGameSessionAwaiter>());
            services.AddTransient<ShellViewModel>();
            services.AddTransient<LibraryViewModel>();
            services.AddTransient<AddGameViewModel>();
            services.AddTransient<GameDetailsViewModel>();
            services.AddTransient<HistoryViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<OnboardingViewModel>();
            services.AddTransient<ConflictDialogViewModel>();
            services.AddTransient<LauncherViewModel>();
            services.AddTransient<SteamImportViewModel>();
        });

    public static IServiceProvider ConfigureHeadless() =>
        Build(services =>
        {
            services.AddSingleton<IGameSessionAwaiter, ConsoleGameSessionAwaiter>();
        });

    private static IServiceProvider Build(Action<ServiceCollection> configureAppServices)
    {
        var services = new ServiceCollection();
        services.AddGameSyncInfrastructure();
        configureAppServices(services);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information));
        return services.BuildServiceProvider();
    }
}
