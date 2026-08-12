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
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace GameSync.App.ViewModels;

public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly IMachineConfigurationStore _machineStore;
    private readonly ISharedGamesConfigurationStore _gamesStore;
    private readonly ISyncWorkflow _syncWorkflow;
    private readonly INavigationService _navigation;
    private readonly AppActivationService _activation;
    private readonly AppActivityService _activity;
    private readonly ILogger<LibraryViewModel> _logger;
    private int _refreshGeneration;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public LibraryViewModel(
        IMachineConfigurationStore machineStore,
        ISharedGamesConfigurationStore gamesStore,
        ISyncWorkflow syncWorkflow,
        INavigationService navigation,
        AppActivationService activation,
        AppActivityService activity,
        ILogger<LibraryViewModel> logger)
    {
        _machineStore = machineStore;
        _gamesStore = gamesStore;
        _syncWorkflow = syncWorkflow;
        _navigation = navigation;
        _activation = activation;
        _activity = activity;
        _logger = logger;
    }

    public ObservableCollection<GameCardItem> Games { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyMessage))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyMessage))]
    public partial bool IsEmpty { get; set; } = true;

    public bool ShowEmptyMessage => IsEmpty && !IsLoading;

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
        if (!await _refreshLock.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        var dispatcher = DispatcherQueue.GetForCurrentThread()
                         ?? App.MainWindow?.DispatcherQueue;
        var generation = Interlocked.Increment(ref _refreshGeneration);
        MachineConfiguration? machine = null;
        IsLoading = true;
        using var activity = _activity.Begin(
            AppActivityKind.Library,
            "Library",
            "Loading your games…",
            15,
            isIndeterminate: true);
        try
        {
            Games.Clear();
            machine = await _machineStore.LoadAsync().ConfigureAwait(true);
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
                Games.Add(new GameCardItem(
                    game.Id,
                    game.Title,
                    game.CoverUrl,
                    GameLibraryStatus.Unknown,
                    lastSyncedText: null,
                    hasWarning: !hasLaunch,
                    warningText: !hasLaunch ? "Executable not configured on this PC" : null,
                    launchAsync: LaunchGameAsync,
                    openDetailsAsync: OpenDetailsAsync));
            }

            IsEmpty = Games.Count == 0;
            IsInfoBarOpen = false;
            activity.Report("Loaded games", 45, isIndeterminate: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh library");
            ShowInfo("Could not load library", Explain(ex), InfoBarSeverity.Error);
            IsEmpty = Games.Count == 0;
            return;
        }
        finally
        {
            // Show cards immediately; sync badges are resolved in the background.
            IsLoading = false;
            _refreshLock.Release();
        }

        if (Games.Count > 0 && dispatcher is not null && machine is not null)
        {
            activity.Report("Checking sync status…", 55, isIndeterminate: true);
            await LoadStatusesAsync(generation, machine, dispatcher, activity).ConfigureAwait(true);
        }
    }

    private async Task LoadStatusesAsync(
        int generation,
        MachineConfiguration machine,
        DispatcherQueue dispatcher,
        AppActivityService.ActivityHandle activity)
    {
        IReadOnlyDictionary<string, SyncStatus> statuses;
        try
        {
            statuses = await _syncWorkflow.GetGameStatusesAsync().ConfigureAwait(false);
            if (generation != _refreshGeneration)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background library status refresh failed");
            return;
        }

        if (generation != _refreshGeneration)
        {
            return;
        }

        await RunOnDispatcherAsync(dispatcher, () =>
        {
            if (generation != _refreshGeneration)
            {
                return;
            }

            ApplyStatuses(statuses, machine);
            activity.Report("Library ready", 100, isIndeterminate: false);
            _logger.LogInformation("Library sync badges updated for {Count} game(s)", Games.Count);
        }).ConfigureAwait(true);
    }

    private static Task RunOnDispatcherAsync(DispatcherQueue dispatcher, Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(new InvalidOperationException("Could not schedule a UI update for library statuses."));
        }

        return tcs.Task;
    }

    private void ApplyStatuses(IReadOnlyDictionary<string, SyncStatus> statuses, MachineConfiguration machine)
    {
        foreach (var item in Games)
        {
            var hasLaunch = machine.Games.TryGetValue(item.Id, out var launch)
                         && LaunchTarget.IsConfigured(launch.Executable);
            var syncStatus = statuses.TryGetValue(item.Id, out var status)
                ? status
                : SyncStatus.Unknown;
            item.Status = GameLibraryStatusMapper.FromSyncStatus(syncStatus, hasLaunch);
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
