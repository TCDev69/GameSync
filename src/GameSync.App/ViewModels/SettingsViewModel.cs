using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameSync.App.Services;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Abstractions.Updates;
using GameSync.Core.Models;
using GameSync.Core.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;

namespace GameSync.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IMachineConfigurationStore _machineStore;
    private readonly IUiSettingsStore _uiSettingsStore;
    private readonly IGitHubAuthenticationService _auth;
    private readonly IGitHubService _gitHub;
    private readonly ILocalAppDataPaths _paths;
    private readonly IThemeService _themeService;
    private readonly IAppUpdateService _updateService;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _suppressUpdatePreferenceSave;

    public SettingsViewModel(
        IMachineConfigurationStore machineStore,
        IUiSettingsStore uiSettingsStore,
        IGitHubAuthenticationService auth,
        IGitHubService gitHub,
        ILocalAppDataPaths paths,
        IThemeService themeService,
        IAppUpdateService updateService,
        ILogger<SettingsViewModel> logger)
    {
        _machineStore = machineStore;
        _uiSettingsStore = uiSettingsStore;
        _auth = auth;
        _gitHub = gitHub;
        _paths = paths;
        _themeService = themeService;
        _updateService = updateService;
        _logger = logger;
    }

    [ObservableProperty]
    public partial string GitHubAccount { get; set; } = "Not signed in";

    [ObservableProperty]
    public partial string Repository { get; set; } = "—";

    [ObservableProperty]
    public partial string Branch { get; set; } = "—";

    [ObservableProperty]
    public partial string MachineId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double MaxBackupsPerGame { get; set; } = 10;

    [ObservableProperty]
    public partial bool BackupEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = "System";

    [ObservableProperty]
    public partial string LogsPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AboutText { get; set; } = "GameSync";

    [ObservableProperty]
    public partial string UpdateStatus { get; set; } = "Not checked yet.";

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial double UpdateProgress { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsInfoBarOpen { get; set; }

    [ObservableProperty]
    public partial string InfoBarTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InfoBarMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity InfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial bool IsSignedIn { get; set; }

    [ObservableProperty]
    public partial bool CheckForUpdatesOnStartup { get; set; } = true;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            _paths.EnsureCreated();
            LogsPath = _paths.LogsDirectory;
            SelectedTheme = _themeService.CurrentTheme;
            AboutText = ResolveAboutText();

            var ui = await _uiSettingsStore.LoadAsync().ConfigureAwait(true);
            _suppressUpdatePreferenceSave = true;
            CheckForUpdatesOnStartup = ui.CheckForUpdatesOnStartup;
            _suppressUpdatePreferenceSave = false;

            var machine = await _machineStore.LoadAsync().ConfigureAwait(true);
            MachineId = machine.MachineId;
            MaxBackupsPerGame = machine.Backup.MaxBackupsPerGame;
            BackupEnabled = machine.Backup.Enabled;
            if (machine.Repository is { } repo)
            {
                Repository = $"{repo.Owner}/{repo.Name}";
                Branch = repo.DefaultBranch;
            }
            else
            {
                Repository = "Not connected";
                Branch = "—";
            }

            IsSignedIn = await _auth.IsAuthenticatedAsync().ConfigureAwait(true);
            if (IsSignedIn)
            {
                try
                {
                    var user = await _gitHub.GetAuthenticatedUserAsync().ConfigureAwait(true);
                    GitHubAccount = string.IsNullOrWhiteSpace(user.Name) ? user.Login : $"{user.Name} (@{user.Login})";
                }
                catch
                {
                    GitHubAccount = "Signed in";
                }
            }
            else
            {
                GitHubAccount = "Not signed in";
            }

            IsInfoBarOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Settings load failed");
            Show("Could not load settings", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _updateService.CheckForUpdatesAsync().ConfigureAwait(true);
            IsUpdateAvailable = result.UpdateAvailable;
            UpdateStatus = result.Message
                           ?? (result.UpdateAvailable
                               ? $"Update available: {result.LatestVersion}"
                               : "You are up to date.");
            Show(
                result.UpdateAvailable ? "Update available" : "No updates",
                UpdateStatus,
                result.UpdateAvailable ? InfoBarSeverity.Informational : InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed");
            UpdateStatus = "Could not check for updates.";
            Show("Update check failed", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        IsBusy = true;
        IsUpdating = true;
        UpdateProgress = 0;
        try
        {
            var progress = new Progress<int>(percent =>
            {
                UpdateProgress = percent;
                UpdateStatus = $"Downloading update… {percent}%";
            });

            var result = await _updateService.UpdateAsync(progress).ConfigureAwait(true);
            UpdateStatus = result.Message ?? $"Installing GameSync {result.Version}.";
            Show(
                "Update installing",
                UpdateStatus + " Your library and saves under LocalAppData are preserved.",
                InfoBarSeverity.Informational);

            if (result.ShouldExitApplication)
            {
                // Inno Setup cannot replace files while GameSync is running; it reopens the app afterwards.
                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(true);
                Microsoft.UI.Xaml.Application.Current?.Exit();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update install failed");
            UpdateStatus = "Update failed.";
            Show("Update failed", ex.Message + " Your library and saves were not modified.", InfoBarSeverity.Error);
        }
        finally
        {
            IsUpdating = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveBackupSettingsAsync()
    {
        try
        {
            var machine = await _machineStore.LoadAsync().ConfigureAwait(true);
            var updated = new MachineConfiguration
            {
                SchemaVersion = machine.SchemaVersion,
                MachineId = machine.MachineId,
                Repository = machine.Repository,
                Games = machine.Games,
                Backup = new BackupSettings
                {
                    Enabled = BackupEnabled,
                    MaxBackupsPerGame = Math.Max(0, (int)MaxBackupsPerGame)
                }
            };
            await _machineStore.SaveAsync(updated).ConfigureAwait(true);
            Show("Backup settings saved", "Retention policy updated for this PC.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Show("Save failed", ex.Message, InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    private async Task SetThemeAsync(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return;
        }

        await _themeService.SetThemeAsync(theme).ConfigureAwait(true);
        SelectedTheme = _themeService.CurrentTheme;
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        IsBusy = true;
        try
        {
            await _auth.SignOutAsync().ConfigureAwait(true);
            IsSignedIn = false;
            GitHubAccount = "Not signed in";
            Show("Signed out", "GitHub credentials were removed from Windows Credential Manager.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Show("Sign out failed", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectRepositoryAsync()
    {
        IsBusy = true;
        try
        {
            var machine = await _machineStore.LoadAsync().ConfigureAwait(true);
            var updated = new MachineConfiguration
            {
                SchemaVersion = machine.SchemaVersion,
                MachineId = machine.MachineId,
                Repository = null,
                Games = machine.Games,
                Backup = machine.Backup
            };
            await _machineStore.SaveAsync(updated).ConfigureAwait(true);

            var ui = await _uiSettingsStore.LoadAsync().ConfigureAwait(true);
            ui.OnboardingCompleted = false;
            await _uiSettingsStore.SaveAsync(ui).ConfigureAwait(true);

            Repository = "Not connected";
            Branch = "—";
            Show("Repository disconnected", "Local launch settings were kept. Run onboarding to connect again.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            Show("Disconnect failed", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            _paths.EnsureCreated();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + LogsPath + "\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Show("Could not open logs", ex.Message, InfoBarSeverity.Error);
        }
    }

    private static string ResolveAboutText() => $"GameSync {AppVersion.Semantic}";

    partial void OnCheckForUpdatesOnStartupChanged(bool value)
    {
        if (_suppressUpdatePreferenceSave)
        {
            return;
        }

        _ = SaveUpdatePreferencesAsync();
    }

    private async Task SaveUpdatePreferencesAsync()
    {
        try
        {
            var ui = await _uiSettingsStore.LoadAsync().ConfigureAwait(true);
            ui.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;
            await _uiSettingsStore.SaveAsync(ui).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save update preferences");
        }
    }

    private void Show(string title, string message, InfoBarSeverity severity)
    {
        InfoBarTitle = title;
        InfoBarMessage = message;
        InfoBarSeverity = severity;
        IsInfoBarOpen = true;
    }
}
