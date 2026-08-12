using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Abstractions.Launch;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Commands;
using GameSync.Core.Models;
using GameSync.Core.Services;
using GameSync.Core.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameSync.App.Cli;

public static class CliCommandRunner
{
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
