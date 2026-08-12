using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameSync.App.Navigation;
using GameSync.App.Services;
using GameSync.App.Views;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Models;
using GameSync.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;

namespace GameSync.App.ViewModels;

public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly IMachineConfigurationStore _machineStore;
    private readonly ISharedGamesConfigurationStore _gamesStore;
    private readonly ISyncWorkflow _syncWorkflow;
    private readonly INavigationService _navigation;
    private readonly AppActivationService _activation;
    private readonly ILogger<LibraryViewModel> _logger;

    public LibraryViewModel(
        IMachineConfigurationStore machineStore,
        ISharedGamesConfigurationStore gamesStore,
        ISyncWorkflow syncWorkflow,
        INavigationService navigation,
        AppActivationService activation,
        ILogger<LibraryViewModel> logger)
    {
        _machineStore = machineStore;
        _gamesStore = gamesStore;
        _syncWorkflow = syncWorkflow;
        _navigation = navigation;
        _activation = activation;
        _logger = logger;
    }

    public ObservableCollection<GameCardItem> Games { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; } = true;

    [ObservableProperty]
    public partial bool IsInfoBarOpen { get; set; }

    [ObservableProperty]
    public partial string InfoBarTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InfoBarMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity InfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            Games.Clear();
            var machine = await _machineStore.LoadAsync().ConfigureAwait(true);
            var repoPath = machine.Repository?.LocalPath;
            if (string.IsNullOrWhiteSpace(repoPath) || !Directory.Exists(repoPath))
            {
                IsEmpty = true;
                ShowInfo("Repository not connected", "Connect a GitHub repository in Settings or complete onboarding.", InfoBarSeverity.Warning);
                return;
            }

            var config = await _gamesStore.LoadAsync(repoPath).ConfigureAwait(true);
            foreach (var game in config.Games.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase))
            {
                var hasLaunch = machine.Games.TryGetValue(game.Id, out var launch)
                             && LaunchTarget.IsConfigured(launch.Executable);
                SyncStatus syncStatus;
                try
                {
                    syncStatus = await _syncWorkflow.GetStatusAsync(game.Id).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Status failed for {GameId}", game.Id);
                    syncStatus = SyncStatus.Failed;
                }

                var libraryStatus = GameLibraryStatusMapper.FromSyncStatus(syncStatus, hasLaunch);
                var warning = !hasLaunch;
                Games.Add(new GameCardItem(
                    game.Id,
                    game.Title,
                    game.CoverUrl,
                    libraryStatus,
                    lastSyncedText: null,
                    hasWarning: warning,
                    warningText: warning ? "Executable not configured on this PC" : null,
                    launchAsync: LaunchGameAsync,
                    openDetailsAsync: OpenDetailsAsync));
            }

            IsEmpty = Games.Count == 0;
            IsInfoBarOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh library");
            ShowInfo("Could not load library", Explain(ex), InfoBarSeverity.Error);
            IsEmpty = Games.Count == 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddGame() => _navigation.NavigateTo(typeof(AddGamePage));

    [RelayCommand]
    private void ImportFromSteam() => _navigation.NavigateTo(typeof(SteamImportPage));

    private Task LaunchGameAsync(string gameId)
    {
        var item = Games.FirstOrDefault(g => g.Id == gameId);
        if (item?.Status == GameLibraryStatus.NotConfigured)
        {
            ShowInfo(
                "Not configured on this PC",
                "Set the game executable in Game details before launching.",
                InfoBarSeverity.Warning);
            return OpenDetailsAsync(gameId);
        }

        _activation.ShowLauncher(gameId);
        return Task.CompletedTask;
    }

    private Task OpenDetailsAsync(string gameId)
    {
        _navigation.NavigateTo(typeof(GameDetailsPage), gameId);
        return Task.CompletedTask;
    }

    private void ShowInfo(string title, string message, InfoBarSeverity severity)
    {
        InfoBarTitle = title;
        InfoBarMessage = message;
        InfoBarSeverity = severity;
        IsInfoBarOpen = true;
    }

    private static string Explain(Exception ex) => ex switch
    {
        DirectoryNotFoundException => "The local repository folder is missing. Reconnect the repository in Settings.",
        UnauthorizedAccessException => "GameSync could not read the configuration files. Check folder permissions.",
        _ => ex.Message
    };
}
