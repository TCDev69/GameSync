using GameSync.App.Navigation;
using GameSync.App.ViewModels;
using GameSync.App.Views;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GameSync.App;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigationService;
    private readonly AppCommand _launchCommand;
    private bool _onboardingActive;

    public MainWindow(AppCommand launchCommand)
    {
        _launchCommand = launchCommand;
        InitializeComponent();

        App.MainWindow = this;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 960;
            presenter.PreferredMinimumHeight = 640;
        }

        _navigationService = App.Services.GetRequiredService<INavigationService>();
        _navigationService.Initialize(ContentFrame);

        Activated += async (_, _) =>
        {
            // Apply theme to root once content is ready.
            var theme = App.Services.GetRequiredService<Services.IThemeService>();
            await theme.InitializeAsync();
        };

        _ = InitializeShellAsync();
    }

    private async Task InitializeShellAsync()
    {
        var needsOnboarding = await NeedsOnboardingAsync().ConfigureAwait(true);
        if (needsOnboarding && _launchCommand.Kind is AppCommandKind.Dashboard or AppCommandKind.Settings)
        {
            ShowOnboarding();
            return;
        }

        ShowMainNavigation();
    }

    private async Task<bool> NeedsOnboardingAsync()
    {
        try
        {
            var ui = await App.Services.GetRequiredService<IUiSettingsStore>().LoadAsync().ConfigureAwait(true);
            if (ui.OnboardingCompleted)
            {
                var machine = await App.Services.GetRequiredService<IMachineConfigurationStore>().LoadAsync().ConfigureAwait(true);
                return string.IsNullOrWhiteSpace(machine.Repository?.LocalPath);
            }

            var auth = App.Services.GetRequiredService<IGitHubAuthenticationService>();
            var signedIn = await auth.IsAuthenticatedAsync().ConfigureAwait(true);
            if (!signedIn)
            {
                return true;
            }

            var machineConfig = await App.Services.GetRequiredService<IMachineConfigurationStore>().LoadAsync().ConfigureAwait(true);
            return string.IsNullOrWhiteSpace(machineConfig.Repository?.LocalPath);
        }
        catch
        {
            return true;
        }
    }

    private void ShowOnboarding()
    {
        _onboardingActive = true;
        RootNavigation.Visibility = Visibility.Collapsed;
        OnboardingFrame.Visibility = Visibility.Visible;
        OnboardingFrame.Navigate(typeof(OnboardingPage));
    }

    public void ShowMainNavigation()
    {
        _onboardingActive = false;
        OnboardingFrame.Visibility = Visibility.Collapsed;
        RootNavigation.Visibility = Visibility.Visible;

        switch (_launchCommand.Kind)
        {
            case AppCommandKind.Settings:
                RootNavigation.SelectedItem = RootNavigation.SettingsItem;
                _navigationService.NavigateTo(typeof(SettingsPage));
                break;
            case AppCommandKind.Status:
            case AppCommandKind.SyncAll:
            case AppCommandKind.SyncGame:
                SelectNavTag("History");
                _navigationService.NavigateTo(typeof(HistoryPage));
                break;
            default:
                RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
                _navigationService.NavigateTo(typeof(LibraryPage));
                break;
        }
    }

    private void SelectNavTag(string tag)
    {
        foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Tag is string value && value == tag)
            {
                RootNavigation.SelectedItem = item;
                return;
            }
        }
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_onboardingActive)
        {
            return;
        }

        if (args.IsSettingsSelected)
        {
            _navigationService.NavigateTo(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            switch (tag)
            {
                case "Library":
                    _navigationService.NavigateTo(typeof(LibraryPage));
                    break;
                case "History":
                    _navigationService.NavigateTo(typeof(HistoryPage));
                    break;
            }
        }
    }
}
