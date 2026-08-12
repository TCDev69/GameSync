using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Abstractions.Launch;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Abstractions.Updates;
using GameSync.Core.Commands;
using GameSync.Core.Models;
using GameSync.Core.Services;
using GameSync.Core.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameSync.App.Cli;

public static class CliCommandRunner
{
    public const int UpdateAvailableExitCode = 10;

    public static bool IsHeadless(AppCommand command) =>
        command.Kind is not AppCommandKind.Dashboard;

    public static async Task<int> RunAsync(IServiceProvider services, AppCommand command, CancellationToken cancellationToken = default)
    {
        return command.Kind switch
        {
            AppCommandKind.Help => RunHelp(),
            AppCommandKind.Status => await RunStatusAsync(services, cancellationToken).ConfigureAwait(false),
            AppCommandKind.Settings => await RunSettingsAsync(services, cancellationToken).ConfigureAwait(false),
            AppCommandKind.SyncAll => await RunSyncAsync(services, gameId: null, cancellationToken).ConfigureAwait(false),
            AppCommandKind.SyncGame => await RunSyncAsync(services, command.GameId, cancellationToken).ConfigureAwait(false),
            AppCommandKind.LaunchGame => await RunLaunchAsync(services, command.GameId!, cancellationToken).ConfigureAwait(false),
            AppCommandKind.CheckUpdate => await RunCheckUpdateAsync(services, cancellationToken).ConfigureAwait(false),
            AppCommandKind.InstallUpdate => await RunInstallUpdateAsync(services, cancellationToken).ConfigureAwait(false),
            _ => RunHelp()
        };
    }

    private static int RunHelp()
    {
        Console.WriteLine(AppCommandParser.GetHelpText());
        return 0;
    }

    private static async Task<int> RunStatusAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var sync = services.GetRequiredService<ISyncWorkflow>();
        var machineStore = services.GetRequiredService<IMachineConfigurationStore>();
        var gamesStore = services.GetRequiredService<ISharedGamesConfigurationStore>();

        var machine = await machineStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var repo = machine.Repository;
        if (repo?.LocalPath is null || !Directory.Exists(repo.LocalPath))
        {
            Console.Error.WriteLine("Repository is not connected on this machine.");
            return 1;
        }

        Console.WriteLine($"Repository: {repo.Owner}/{repo.Name}");
        Console.WriteLine($"Local path: {repo.LocalPath}");
        Console.WriteLine($"Overall status: {await sync.GetStatusAsync(cancellationToken: cancellationToken).ConfigureAwait(false)}");
        Console.WriteLine();

        var games = await gamesStore.LoadAsync(repo.LocalPath, cancellationToken).ConfigureAwait(false);
        foreach (var game in games.Games.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            var hasLaunch = machine.Games.TryGetValue(game.Id, out var launch)
                            && LaunchTarget.IsConfigured(launch.Executable);
            var status = await sync.GetStatusAsync(game.Id, cancellationToken).ConfigureAwait(false);
            var launchText = hasLaunch ? launch!.Executable.Trim() : "(not configured)";
            Console.WriteLine($"- {game.Id}: {game.Title}");
            Console.WriteLine($"    status: {status}");
            Console.WriteLine($"    launch: {launchText}");
        }

        return 0;
    }

    private static async Task<int> RunSettingsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var machineStore = services.GetRequiredService<IMachineConfigurationStore>();
        var auth = services.GetRequiredService<IGitHubAuthenticationService>();
        var machine = await machineStore.LoadAsync(cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"GameSync {AppVersion.Semantic}");
        Console.WriteLine($"Machine ID: {machine.MachineId}");
        Console.WriteLine($"GitHub signed in: {(await auth.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false) ? "yes" : "no")}");

        if (machine.Repository is { } repo)
        {
            Console.WriteLine($"Repository: {repo.Owner}/{repo.Name}");
            Console.WriteLine($"Local clone: {repo.LocalPath ?? "(not cloned)"}");
            Console.WriteLine($"Default branch: {repo.DefaultBranch}");
        }
        else
        {
            Console.WriteLine("Repository: (not connected)");
        }

        Console.WriteLine($"Backup max per game: {machine.Backup.MaxBackupsPerGame}");
        Console.WriteLine($"Configured launch profiles: {machine.Games.Count}");
        Console.WriteLine();
        Console.WriteLine("Data directory: " + Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\GameSync");
        return 0;
    }

    private static async Task<int> RunSyncAsync(IServiceProvider services, string? gameId, CancellationToken cancellationToken)
    {
        var sync = services.GetRequiredService<ISyncWorkflow>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("CliCommandRunner");

        SyncResult result = string.IsNullOrWhiteSpace(gameId)
            ? await sync.SyncAllAsync(cancellationToken).ConfigureAwait(false)
            : await sync.SyncGameAsync(gameId, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("CLI sync finished succeeded={Succeeded} message={Message}", result.Succeeded, result.Message);
        Console.WriteLine(result.Succeeded ? "OK" : "FAILED");
        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            Console.WriteLine(result.Message);
        }

        return result.Succeeded ? 0 : 1;
    }

    /// <summary>
    /// Exit codes: 0 already current, <see cref="UpdateAvailableExitCode"/> update available, 1 check failed.
    /// </summary>
    private static async Task<int> RunCheckUpdateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var updates = services.GetRequiredService<IAppUpdateService>();
        var result = await updates.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Installed version: {result.CurrentVersion}");
        Console.WriteLine($"Latest release:    {result.LatestVersion ?? "(unknown)"}");
        Console.WriteLine($"Update available:  {(result.UpdateAvailable ? "yes" : "no")}");

        if (result.InstallerUri is { } installer)
        {
            Console.WriteLine($"Installer:         {installer}");
            Console.WriteLine($"Size:              {FormatSize(result.InstallerSizeBytes)}");
            Console.WriteLine($"SHA-256:           {result.InstallerSha256 ?? "(not published)"}");
        }

        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            Console.WriteLine(result.Message);
        }

        if (result.LatestVersion is null)
        {
            return 1;
        }

        return result.UpdateAvailable ? UpdateAvailableExitCode : 0;
    }

    private static async Task<int> RunInstallUpdateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var updates = services.GetRequiredService<IAppUpdateService>();
        var check = await updates.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
        if (!check.UpdateAvailable)
        {
            Console.WriteLine(check.Message ?? "Already on the latest version.");
            return 0;
        }

        var lastReported = -1;
        var progress = new Progress<int>(percent =>
        {
            // One line per 10% keeps redirected output readable.
            if (percent / 10 == lastReported / 10 && percent != 100)
            {
                return;
            }

            lastReported = percent;
            Console.WriteLine($"Downloading {check.LatestVersion}… {percent}%");
        });

        try
        {
            var result = await updates.UpdateAsync(progress, cancellationToken).ConfigureAwait(false);
            Console.WriteLine(result.Message ?? $"Installing {result.Version}.");
            Console.WriteLine($"Installer: {result.InstallerPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string FormatSize(long? bytes) =>
        bytes is > 0 ? $"{bytes.Value / (1024d * 1024d):F1} MB" : "(unknown)";

    private static async Task<int> RunLaunchAsync(IServiceProvider services, string gameId, CancellationToken cancellationToken)
    {
        var launcher = services.GetRequiredService<IGameLauncher>();
        var progress = new Progress<LaunchProgress>(p => Console.WriteLine($"[{p.Phase}] {p.Message}"));

        var result = await launcher.LaunchAsync(gameId, progress, cancellationToken).ConfigureAwait(false);
        if (result.WasCancelled)
        {
            Console.Error.WriteLine(result.Message ?? "Launch cancelled.");
            return 2;
        }

        if (!result.Succeeded)
        {
            Console.Error.WriteLine(result.Message ?? "Launch failed.");
            return 1;
        }

        Console.WriteLine(result.Message ?? "Launch completed.");
        return 0;
    }
}
