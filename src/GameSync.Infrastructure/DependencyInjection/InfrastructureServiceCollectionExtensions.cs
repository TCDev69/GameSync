using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Git;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Abstractions.Launch;
using GameSync.Core.Abstractions.Metadata;
using GameSync.Core.Abstractions.Repository;
using GameSync.Core.Abstractions.Games;
using GameSync.Core.Abstractions.Shortcuts;
using GameSync.Core.Abstractions.Steam;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Abstractions.Updates;
using GameSync.Core.DependencyInjection;
using GameSync.Core.Options;
using GameSync.Infrastructure.Configuration;
using GameSync.Infrastructure.Credentials;
using GameSync.Infrastructure.Git;
using GameSync.Infrastructure.GitHub;
using GameSync.Infrastructure.Games;
using GameSync.Infrastructure.Launch;
using GameSync.Infrastructure.Steam;
using GameSync.Infrastructure.Logging;
using GameSync.Infrastructure.Metadata;
using GameSync.Infrastructure.Paths;
using GameSync.Infrastructure.Repositories;
using GameSync.Infrastructure.Shortcuts;
using GameSync.Infrastructure.Sync;
using GameSync.Infrastructure.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGameSyncInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGameSyncCore();
        services.AddOptions<GameSyncOptions>()
            .Configure(options =>
            {
                ApplyEnv("GAMESYNC_GITHUB_CLIENT_ID", v => options.GitHubClientId = v);
                ApplyEnv("GAMESYNC_UPDATE_OWNER", v => options.UpdateReleasesOwner = v);
                ApplyEnv("GAMESYNC_UPDATE_REPO", v => options.UpdateReleasesRepo = v);
            });

        services.AddHttpClient("GitHubOAuth", (sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GameSyncOptions>>().Value;
            client.BaseAddress = new Uri(options.GitHubOAuthBaseUrl);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        services.AddHttpClient("GitHubApi", (sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GameSyncOptions>>().Value;
            client.BaseAddress = new Uri(options.GitHubApiBaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GameSync");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });
        services.AddHttpClient("SteamStore", client =>
        {
            client.BaseAddress = new Uri("https://store.steampowered.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GameSync");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.AddSingleton<ILocalAppDataPaths, LocalAppDataPaths>();
        services.AddSingleton<IPathResolver, PathResolver>();
        services.AddSingleton<IMachineConfigurationStore, JsonMachineConfigurationStore>();
        services.AddSingleton<ISharedGamesConfigurationStore, JsonSharedGamesConfigurationStore>();
        services.AddSingleton<IUiSettingsStore, JsonUiSettingsStore>();
        services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
        services.AddSingleton<IUriLauncher, SystemUriLauncher>();
        services.AddSingleton<IGameMetadataProvider, SteamStoreMetadataProvider>();
        services.AddSingleton<ISaveLocationProvider, HeuristicSaveLocationProvider>();

        services.AddSingleton<IGitHubOAuthClient, HttpGitHubOAuthClient>();
        services.AddSingleton<IGitHubApiClient, HttpGitHubApiClient>();
        services.AddSingleton<IGitHubAuthenticationService, GitHubAuthenticationService>();
        services.AddSingleton<IGitHubService, GitHubService>();
        services.AddSingleton<IGitHubRepositoryConnectionService, GitHubRepositoryConnectionService>();

        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IRepositoryService, RepositoryService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<SaveService>();
        services.AddSingleton<ISaveService>(sp => sp.GetRequiredService<SaveService>());
        services.AddSingleton<ISaveSyncService>(sp => sp.GetRequiredService<SaveService>());
        services.AddSingleton<IConflictResolver, ConflictResolver>();
        services.AddSingleton<SyncService>();
        services.AddSingleton<ISyncService>(sp => sp.GetRequiredService<SyncService>());
        services.AddSingleton<ISyncWorkflow>(sp => sp.GetRequiredService<SyncService>());

        services.AddSingleton<WindowsProcessLauncher>();
        services.AddSingleton<IProcessLauncher>(sp => sp.GetRequiredService<WindowsProcessLauncher>());
        services.AddSingleton<IProcessMonitor>(sp => sp.GetRequiredService<WindowsProcessLauncher>());
        services.AddSingleton<GameLauncher>();
        services.AddSingleton<IGameLauncher>(sp => sp.GetRequiredService<GameLauncher>());
        services.AddSingleton<IGameLaunchWorkflow>(sp => sp.GetRequiredService<GameLauncher>());
        services.AddSingleton<IShortcutService, WindowsShortcutService>();
        services.AddSingleton<IShortcutTargetResolver, WindowsShortcutTargetResolver>();
        services.AddSingleton<IAppUpdateService, GitHubReleaseAppUpdateService>();

        services.AddSingleton<ISteamInstalledGamesProvider, WindowsSteamInstalledGamesProvider>();
        services.AddSingleton<IGameRegistrationService, GameRegistrationService>();

        services.AddSingleton<WindowsProtocolLauncher>();
        services.AddSingleton<IProtocolLauncher>(sp => sp.GetRequiredService<WindowsProtocolLauncher>());
        services.AddSingleton<WindowsGameProcessWatcher>();
        services.AddSingleton<IGameProcessWatcher>(sp => sp.GetRequiredService<WindowsGameProcessWatcher>());

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.Services.AddSingleton<ILoggerProvider, GameSyncFileLoggerProvider>();
        });

        services.AddOptions<GameSyncFileLoggerOptions>().Configure<ILocalAppDataPaths>((options, paths) =>
        {
            paths.EnsureCreated();
            options.LogsDirectory = paths.LogsDirectory;
            options.RetentionDays = 14;
            options.MinimumLevel = LogLevel.Information;
        });

        return services;
    }

    private static void ApplyEnv(string name, Action<string> assign)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value))
        {
            assign(value);
        }
    }
}
