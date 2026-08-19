using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameSync.Core.Models;

namespace GameSync.App.ViewModels;

public sealed partial class ConflictDialogViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string GameTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SaveLocation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LocalVersion { get; set; } = "Keep the version on this PC";

    [ObservableProperty]
    public partial string RemoteVersion { get; set; } = "Keep the version from GitHub";

    [ObservableProperty]
    public partial string LocalHint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RemoteHint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LocalButtonText { get; set; } = "Keep local";

    [ObservableProperty]
    public partial string RemoteButtonText { get; set; } = "Use remote";

    [ObservableProperty]
    public partial string? LocalTimestamp { get; set; }

    [ObservableProperty]
    public partial string? RemoteTimestamp { get; set; }

    [ObservableProperty]
    public partial string? LocalMachine { get; set; }

    [ObservableProperty]
    public partial string? RemoteMachine { get; set; }

    [ObservableProperty]
    public partial bool DetailsExpanded { get; set; }

    [ObservableProperty]
    public partial string TechnicalDetails { get; set; } = string.Empty;

    public ConflictResolutionChoice Result { get; private set; } = ConflictResolutionChoice.Cancel;

    public event EventHandler? CloseRequested;

    public void LoadFromConflict(Conflict conflict, string? gameTitle = null)
    {
        GameTitle = gameTitle ?? conflict.GameId ?? "Game";
        SaveLocation = conflict.LocalPath ?? conflict.RemotePath ?? conflict.Path;
        TechnicalDetails =
            $"Path: {conflict.Path}\nType: {conflict.Type}\nBinary: {conflict.IsBinary}\n{conflict.Message}";

        if (conflict.Type == ConflictType.SaveDivergence)
        {
            SummaryText =
                "This PC and GitHub both have different save data. Choose one side to keep. The other side will not be uploaded until you play again.";
            LocalVersion = "Keep saves on this PC";
            RemoteVersion = "Use saves from GitHub";
            LocalHint = "Your current files stay on disk. GameSync will publish them to GitHub.";
            RemoteHint = "Downloads GitHub saves and replaces the files on this PC (a local backup is created first).";
            LocalButtonText = "Keep local saves";
            RemoteButtonText = "Use remote saves";
            return;
        }

        SummaryText =
            "Git reported a merge conflict in the repository. Choose which version to keep in the repo working tree.";
        LocalVersion = "Keep the version on this PC";
        RemoteVersion = "Keep the version from GitHub";
        LocalHint = "Resolves the Git conflict using this PC's copy in the repository.";
        RemoteHint = "Resolves the Git conflict using the copy that came from GitHub.";
        LocalButtonText = "Use local copy";
        RemoteButtonText = "Use remote copy";
    }

    [RelayCommand]
    private void UseLocal()
    {
        Result = ConflictResolutionChoice.UseLocal;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void UseRemote()
    {
        Result = ConflictResolutionChoice.UseRemote;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ViewHistory()
    {
        Result = ConflictResolutionChoice.ViewHistory;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        Result = ConflictResolutionChoice.Cancel;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
