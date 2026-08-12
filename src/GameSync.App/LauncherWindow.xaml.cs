using GameSync.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace GameSync.App;

public sealed partial class LauncherWindow : Window
{
    public LauncherViewModel ViewModel { get; }

    public LauncherWindow(string gameId)
    {
        ViewModel = App.Services.GetRequiredService<LauncherViewModel>();
        ViewModel.GameId = gameId;
        ViewModel.GameTitle = gameId;

        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(480, 320));

        ViewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) =>
        {
            ViewModel.CloseRequested -= OnCloseRequested;
            ViewModel.Cancel();
        };
        Activated += LauncherWindow_Activated;
    }

    private async void LauncherWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= LauncherWindow_Activated;
        if (ViewModel.StartCommand.CanExecute(null))
        {
            await ViewModel.StartCommand.ExecuteAsync(null);
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Cancel();
        Close();
    }
}
