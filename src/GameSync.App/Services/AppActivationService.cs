using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Commands;
using GameSync.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Activation;
using AppInstance = Microsoft.Windows.AppLifecycle.AppInstance;

namespace GameSync.App.Services;

/// <summary>
/// Routes CLI / redirected activations to the dashboard or lightweight launcher window.
/// </summary>
public sealed class AppActivationService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AppActivationService> _logger;
    private Window? _mainWindow;
    private readonly List<Window> _launcherWindows = [];

    public AppActivationService(IServiceProvider services, ILogger<AppActivationService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task HandleCommandAsync(AppCommand command)
    {
        switch (command.Kind)
        {
            case AppCommandKind.LaunchGame:
                if (string.IsNullOrWhiteSpace(command.GameId))
                {
                    throw new ArgumentException("--game requires a game id.");
                }

                ShowLauncher(command.GameId);
                break;

            case AppCommandKind.Help:
                await ShowHelpAsync().ConfigureAwait(true);
                break;

            case AppCommandKind.SyncAll:
            case AppCommandKind.SyncGame:
                await RunSyncCommandAsync(command).ConfigureAwait(true);
                break;

            case AppCommandKind.Status:
                EnsureMainWindow(new AppCommand { Kind = AppCommandKind.Status, RawArguments = command.RawArguments });
                break;

            case AppCommandKind.Settings:
                EnsureMainWindow(command);
                break;

            default:
                EnsureMainWindow(command);
                break;
        }
    }

    public void EnsureMainWindow(AppCommand command)
    {
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow(command);
            App.MainWindow = _mainWindow;
            _mainWindow.Closed += (_, _) =>
            {
                if (ReferenceEquals(App.MainWindow, _mainWindow))
                {
                    App.MainWindow = null;
                }

                _mainWindow = null;
            };
        }

        _mainWindow.Activate();
    }

    public void ShowLauncher(string gameId)
    {
        var window = new LauncherWindow(gameId);
        _launcherWindows.Add(window);
        window.Closed += (_, _) => _launcherWindows.Remove(window);
        window.Activate();
    }

    private async Task RunSyncCommandAsync(AppCommand command)
    {
        EnsureMainWindow(new AppCommand { Kind = AppCommandKind.Status, RawArguments = command.RawArguments });
        var sync = _services.GetRequiredService<ISyncWorkflow>();
        SyncResult result;
        if (command.Kind == AppCommandKind.SyncGame && !string.IsNullOrWhiteSpace(command.GameId))
        {
            result = await sync.SyncGameAsync(command.GameId).ConfigureAwait(true);
        }
        else
        {
            result = await sync.SyncAllAsync().ConfigureAwait(true);
        }

        _logger.LogInformation("CLI sync finished succeeded={Succeeded} message={Message}", result.Succeeded, result.Message);
        if (_mainWindow?.Content is FrameworkElement { XamlRoot: not null } root)
        {
            var dialog = new ContentDialog
            {
                Title = result.Succeeded ? "Sync completed" : "Sync failed",
                Content = result.Message ?? (result.Succeeded ? "OK" : "Sync failed."),
                CloseButtonText = "OK",
                XamlRoot = root.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private async Task ShowHelpAsync()
    {
        EnsureMainWindow(new AppCommand { Kind = AppCommandKind.Dashboard, RawArguments = Array.Empty<string>() });
        if (_mainWindow?.Content is FrameworkElement { XamlRoot: not null } root)
        {
            var dialog = new ContentDialog
            {
                Title = "GameSync help",
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = AppCommandParser.GetHelpText(),
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
                },
                CloseButtonText = "OK",
                XamlRoot = root.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    public void RegisterActivatedHandler(DispatcherQueue dispatcherQueue)
    {
        AppInstance.GetCurrent().Activated += (_, args) =>
        {
            dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var command = ParseActivation(args.Data);
                    await HandleCommandAsync(command).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle redirected activation");
                }
            });
        };
    }

    public static AppCommand ParseActivation(object? activatedArgs)
    {
        try
        {
            if (activatedArgs is ILaunchActivatedEventArgs launchArgs
                && !string.IsNullOrWhiteSpace(launchArgs.Arguments))
            {
                var parts = CommandLineToArgs(launchArgs.Arguments);
                return AppCommandParser.Parse(parts);
            }
        }
        catch (Exception)
        {
            // Fall through to process command line.
        }

        return ParseCurrentCommandLine();
    }

    public static AppCommand ParseCurrentCommandLine()
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        try
        {
            return AppCommandParser.Parse(args);
        }
        catch
        {
            return new AppCommand { Kind = AppCommandKind.Help, RawArguments = args };
        }
    }

    private static string[] CommandLineToArgs(string commandLine) =>
        string.IsNullOrWhiteSpace(commandLine)
            ? []
            : commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
