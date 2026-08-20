using GameSync.App.Navigation;
using GameSync.App.Services;
using GameSync.App.ViewModels;
using GameSync.App.Views;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Abstractions.Updates;
using GameSync.Core.Commands;
using GameSync.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GameSync.App;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigationService;
    private readonly TaskbarProgressService _taskbarProgress;
    private readonly AppCommand _launchCommand;
    private bool _onboardingActive;
    private bool _suppressSelectionNavigation;

    public AppActivityService Activity { get; }

    public MainWindow(AppCommand launchCommand)
    {
        _launchCommand = launchCommand;
        Activity = App.Services.GetRequiredService<AppActivityService>();
        _taskbarProgress = App.Services.GetRequiredService<TaskbarProgressService>();
        InitializeComponent();
        ActivityPanel.DataContext = Activity;

        App.MainWindow = this;
        _taskbarProgress.Register(this);
        Closed += (_, _) => _taskbarProgress.Unregister(this);
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
        ContentFrame.NavigationFailed += ContentFrame_NavigationFailed;

        Activated += async (_, _) =>
        {
            // Apply theme to root once content is ready.
            var theme = App.Services.GetRequiredService<Services.IThemeService>();
            await theme.InitializeAsync();
        };

        _ = InitializeShellSafeAsync();
    }

    private async Task InitializeShellSafeAsync()
    {
        var logger = App.Services.GetRequiredService<ILogger<MainWindow>>();
        try
        {
            await InitializeShellAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Shell initialization failed; opening library as fallback");
            if (!_onboardingActive)
            {
                ShowMainNavigation();
            }
        }
    }

    private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        App.Services.GetRequiredService<ILogger<MainWindow>>()
            .LogError(e.Exception, "Navigation to {Page} failed", e.SourcePageType.Name);
        e.Handled = true;
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
        await NotifyIfUpdateAvailableAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Silent background check: an offline machine must never see an error here, and nothing is
    /// downloaded until the user accepts from the banner or from Settings.
    /// </summary>
    private async Task NotifyIfUpdateAvailableAsync()
    {
        try
        {
            var options = App.Services.GetRequiredService<IOptions<GameSyncOptions>>().Value;
            if (!options.CheckForUpdatesOnStartup)
            {
                return;
            }

            var ui = await App.Services.GetRequiredService<IUiSettingsStore>().LoadAsync().ConfigureAwait(true);
            if (!ui.CheckForUpdatesOnStartup)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(true);

            var updates = App.Services.GetRequiredService<IAppUpdateService>();
            var result = await updates.CheckForUpdatesAsync().ConfigureAwait(true);
            if (!result.UpdateAvailable)
            {
                return;
            }

            UpdateBar.Message =
                $"GameSync {result.LatestVersion} is available (you have {result.CurrentVersion}). "
                + "Your library and saves are preserved.";
            UpdateBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            App.Services.GetRequiredService<ILogger<MainWindow>>()
                .LogDebug(ex, "Startup update check skipped");
        }
    }

    private void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        UpdateBar.IsOpen = false;

        // Selecting the item would navigate without the parameter first.
        _suppressSelectionNavigation = true;
        RootNavigation.SelectedItem = RootNavigation.SettingsItem;
        _suppressSelectionNavigation = false;

        _navigationService.NavigateTo(typeof(SettingsPage), SettingsPage.InstallUpdateParameter);
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
                _suppressSelectionNavigation = true;
                RootNavigation.SelectedItem = RootNavigation.SettingsItem;
                _suppressSelectionNavigation = false;
                _navigationService.NavigateTo(typeof(SettingsPage));
                break;
            case AppCommandKind.Status:
            case AppCommandKind.SyncAll:
            case AppCommandKind.SyncGame:
                SelectNavTag("History");
                _navigationService.NavigateTo(typeof(HistoryPage));
                break;
            default:
                _suppressSelectionNavigation = true;
                RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
                _suppressSelectionNavigation = false;
                _navigationService.NavigateTo(typeof(LibraryPage));
                break;
        }
    }

    public void ShowLibraryHome()
    {
        if (_onboardingActive)
        {
            return;
        }

        _suppressSelectionNavigation = true;
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        _suppressSelectionNavigation = false;
        _navigationService.NavigateTo(typeof(LibraryPage));
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
        if (_onboardingActive || _suppressSelectionNavigation)
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
