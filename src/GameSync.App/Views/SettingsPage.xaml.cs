using GameSync.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GameSync.App.Views;

public sealed partial class SettingsPage : Page
{
    /// <summary>
    /// Navigation parameter that starts the update install right away, so the startup banner
    /// stays a single click while the download progress remains visible here.
    /// </summary>
    public const string InstallUpdateParameter = "install-update";

    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.LoadCommand.Execute(null);

        if (e.Parameter as string == InstallUpdateParameter)
        {
            ViewModel.IsUpdateAvailable = true;
            ViewModel.InstallUpdateCommand.Execute(null);
        }
    }
}
