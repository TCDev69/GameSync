using GameSync.App.Services;
using GameSync.Core.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace GameSync.App;

public partial class App : Application
{
    private AppActivationService? _activationService;

    public static IServiceProvider Services { get; private set; } = null!;

    public static Window? MainWindow { get; set; }

    public App()
    {
        InitializeComponent();
        Services = AppServices.Configure();
        CrashHandler.Register(this);
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Application startup version={Version}", Core.Versioning.AppVersion.Semantic);

        var theme = Services.GetRequiredService<IThemeService>();
        await theme.InitializeAsync();

        _activationService = Services.GetRequiredService<AppActivationService>();
        _activationService.RegisterActivatedHandler(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

        AppCommand command;
        try
        {
            var activated = AppInstance.GetCurrent().GetActivatedEventArgs();
            command = AppActivationService.ParseActivation(activated.Data);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falling back to process command line");
            command = AppActivationService.ParseCurrentCommandLine();
        }

        logger.LogInformation("Launch mode {CommandKind} game={GameId}", command.Kind, command.GameId);
        await _activationService.HandleCommandAsync(command);
    }
}
