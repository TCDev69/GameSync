using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GameSync.App.Services;

/// <summary>
/// Global exception handling for WinUI. Logs failures and surfaces a non-sensitive dialog.
/// </summary>
public static class CrashHandler
{
    private static int _dialogVisible;

    public static void Register(Application app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UnhandledException += OnUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            LogFatal(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogFatal(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };
    }

    private static async void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogFatal(e.Exception, "Application.UnhandledException");
        e.Handled = true;

        try
        {
            await ShowUserDialogAsync(SanitizeMessage(e.Exception)).ConfigureAwait(true);
        }
        catch
        {
            // Last-resort: avoid recursive crash loops.
        }
    }

    private static void LogFatal(Exception? exception, string source)
    {
        try
        {
            var logger = App.Services.GetService<ILoggerFactory>()?.CreateLogger("CrashHandler");
            logger?.LogCritical(exception, "Unhandled exception ({Source})", source);
        }
        catch
        {
            // Ignore logging failures during crash handling.
        }
    }

    private static string SanitizeMessage(Exception? exception)
    {
        if (exception is null)
        {
            return "An unexpected error occurred.";
        }

        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message)
            || message.Contains("access_token", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
            || message.Contains("gho_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ghp_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ghu_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ghs_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("github_pat_", StringComparison.OrdinalIgnoreCase))
        {
            return "An unexpected error occurred. Details were written to the GameSync log folder.";
        }

        // Keep it short for the dialog; full details stay in logs.
        return message.Length > 280 ? message[..280] + "…" : message;
    }

    private static async Task ShowUserDialogAsync(string message)
    {
        if (Interlocked.Exchange(ref _dialogVisible, 1) == 1)
        {
            return;
        }

        try
        {
            var root = App.MainWindow?.Content as FrameworkElement;
            if (root?.XamlRoot is null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Something went wrong",
                Content = message + Environment.NewLine + Environment.NewLine
                          + "GameSync logged the error under %LOCALAPPDATA%\\GameSync\\logs\\.",
                CloseButtonText = "OK",
                XamlRoot = root.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            Interlocked.Exchange(ref _dialogVisible, 0);
        }
    }
}
