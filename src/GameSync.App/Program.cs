using GameSync.App.Cli;
using GameSync.App.Services;
using GameSync.Core.Abstractions.Launch;
using GameSync.Core.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinRT;

namespace GameSync.App;

public static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();

        AppCommand command;
        try
        {
            command = AppCommandParser.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(AppCommandParser.GetHelpText());
            return 1;
        }

        if (CliCommandRunner.IsHeadless(command))
        {
            var services = AppServices.ConfigureHeadless();
            return await CliCommandRunner.RunAsync(services, command).ConfigureAwait(false);
        }

        var keyInstance = AppInstance.FindOrRegisterForKey("GameSync.Main");
        if (!keyInstance.IsCurrent)
        {
            var activated = AppInstance.GetCurrent().GetActivatedEventArgs();
            await keyInstance.RedirectActivationToAsync(activated).AsTask().ConfigureAwait(false);
            return 0;
        }

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }
}
