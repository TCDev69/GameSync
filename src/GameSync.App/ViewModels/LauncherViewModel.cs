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
    private readonly ILogger<LauncherViewModel> _logger;
    private CancellationTokenSource? _cts;

    public LauncherViewModel(IGameLauncher gameLauncher, UiGameSessionAwaiter sessionAwaiter, ILogger<LauncherViewModel> logger)
    {
        _gameLauncher = gameLauncher;
        _sessionAwaiter = sessionAwaiter;
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
    public partial bool IsError { get; set; }

    [ObservableProperty]
    public partial string? ProcessIdText { get; set; }

    [ObservableProperty]
    public partial bool AwaitingSessionEnd { get; set; }

    [RelayCommand]
    private void ConfirmSessionEnd() => _sessionAwaiter.CompleteSession();

    [RelayCommand]
    private async Task StartAsync()
    {
        if (string.IsNullOrWhiteSpace(GameId) || IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsError = false;
        _cts = new CancellationTokenSource();
        var progress = new Progress<LaunchProgress>(OnProgress);

        try
        {
            var result = await _gameLauncher.LaunchAsync(GameId, progress, _cts.Token).ConfigureAwait(true);
            if (result.Succeeded)
            {
                Phase = LaunchPhase.Completed;
                StatusMessage = "Completed";
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (!result.WasCancelled)
            {
                IsError = true;
                StatusMessage = result.Message ?? "Launch failed.";
                Phase = LaunchPhase.Error;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Launcher UI failed for {GameId}", GameId);
            IsError = true;
            Phase = LaunchPhase.Error;
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    public event EventHandler? CloseRequested;

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
        }

        ProcessIdText = progress.ProcessId is int pid ? $"PID {pid}" : null;
        IsError = progress.Phase == LaunchPhase.Error;
        AwaitingSessionEnd = progress.Phase == LaunchPhase.AwaitingSessionEnd;
    }
}
