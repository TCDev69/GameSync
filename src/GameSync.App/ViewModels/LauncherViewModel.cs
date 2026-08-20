using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameSync.App.Services;
using GameSync.Core.Abstractions.Launch;
using GameSync.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameSync.App.ViewModels;

public sealed partial class LauncherViewModel : ObservableObject
{
    private readonly IGameLauncher _gameLauncher;
    private readonly UiGameSessionAwaiter _sessionAwaiter;
    private readonly AppActivityService _activity;
    private readonly ILogger<LauncherViewModel> _logger;
    private CancellationTokenSource? _cts;
    private AppActivityService.ActivityHandle? _activityHandle;

    public LauncherViewModel(
        IGameLauncher gameLauncher,
        UiGameSessionAwaiter sessionAwaiter,
        AppActivityService activity,
        ILogger<LauncherViewModel> logger)
    {
        _gameLauncher = gameLauncher;
        _sessionAwaiter = sessionAwaiter;
        _activity = activity;
        _logger = logger;
    }

    [ObservableProperty]
    public partial string GameId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GameTitle { get; set; } = "Game";

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Preparing";

    [ObservableProperty]
    public partial LaunchPhase Phase { get; set; } = LaunchPhase.Preparing;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; set; }

    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; set; }

    [ObservableProperty]
    public partial bool IsError { get; set; }

    [ObservableProperty]
    public partial string? ProcessIdText { get; set; }

    [ObservableProperty]
    public partial bool AwaitingSessionEnd { get; set; }

    [RelayCommand]
    private void ConfirmSessionEnd() => _sessionAwaiter.CompleteSession();

    [RelayCommand]
    private Task StartAsync() => RunLaunchAsync();

    /// <summary>Called after the user resolves a save-divergence dialog so launch continues without reopening the app.</summary>
    public Task RetryLaunchAfterConflictAsync()
    {
        IsError = false;
        IsBusy = false;
        return RunLaunchAsync();
    }

    private async Task RunLaunchAsync()
    {
        if (string.IsNullOrWhiteSpace(GameId) || IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsError = false;
        _cts = new CancellationTokenSource();
        var progress = new Progress<LaunchProgress>(OnProgress);
        _activityHandle = _activity.Begin(
            AppActivityKind.Launch,
            GameTitle,
            LaunchPhaseProgress.Describe(LaunchPhase.Preparing, null),
            5);

        try
        {
            var result = await _gameLauncher.LaunchAsync(GameId, progress, _cts.Token).ConfigureAwait(true);
            if (result.Succeeded)
            {
                Phase = LaunchPhase.Completed;
                StatusMessage = "Completed";
                ApplyProgress(LaunchPhase.Completed, StatusMessage);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (!result.WasCancelled)
            {
                var syncConflict = result.PreLaunchSync ?? result.PostExitSync;
                if (syncConflict is not null && syncConflict.Conflicts.Count > 0)
                {
                    if (syncConflict.Conflicts[0].Type == ConflictType.SaveDivergence)
                    {
                        IsError = false;
                        StatusMessage = "Local and remote saves differ. Choose which version to keep.";
                        ApplyProgress(LaunchPhase.DownloadingSaves, StatusMessage);
                    }

                    ConflictDetected?.Invoke(this, syncConflict);
                    return;
                }

                IsError = true;
                StatusMessage = result.Message ?? "Launch failed.";
                Phase = LaunchPhase.Error;
                ApplyProgress(LaunchPhase.Error, StatusMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Launcher UI failed for {GameId}", GameId);
            IsError = true;
            Phase = LaunchPhase.Error;
            StatusMessage = ex.Message;
            ApplyProgress(LaunchPhase.Error, StatusMessage);
        }
        finally
        {
            IsBusy = false;
            _activityHandle?.Dispose();
            _activityHandle = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public event EventHandler? CloseRequested;
    public event EventHandler<SyncResult>? ConflictDetected;

    public void Cancel()
    {
        _cts?.Cancel();
    }

    private void OnProgress(LaunchProgress progress)
    {
        Phase = progress.Phase;
        StatusMessage = progress.Message;
        if (!string.IsNullOrWhiteSpace(progress.GameTitle))
        {
            GameTitle = progress.GameTitle;
            _activityHandle?.SetTitle(progress.GameTitle);
        }

        ProcessIdText = progress.ProcessId is int pid ? $"PID {pid}" : null;
        IsError = progress.Phase == LaunchPhase.Error;
        AwaitingSessionEnd = progress.Phase == LaunchPhase.AwaitingSessionEnd;
        ApplyProgress(progress.Phase, progress.Message);
    }

    private void ApplyProgress(LaunchPhase phase, string? message)
    {
        var (percent, indeterminate) = LaunchPhaseProgress.Map(phase);
        ProgressPercent = percent;
        IsProgressIndeterminate = indeterminate;
        _activityHandle?.Report(
            LaunchPhaseProgress.Describe(phase, message),
            percent,
            indeterminate,
            phase == LaunchPhase.Error);
    }
}
