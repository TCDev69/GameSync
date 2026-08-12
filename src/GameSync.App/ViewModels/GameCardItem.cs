using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameSync.Core.Models;
using GameSync.Core.Services;

namespace GameSync.App.ViewModels;

public sealed partial class GameCardItem : ObservableObject
{
    private readonly Func<string, Task>? _launchAsync;
    private readonly Func<string, Task>? _openDetailsAsync;

    public GameCardItem(
        string id,
        string title,
        string? coverUrl,
        GameLibraryStatus status,
        string? lastSyncedText = null,
        bool hasWarning = false,
        string? warningText = null,
        Func<string, Task>? launchAsync = null,
        Func<string, Task>? openDetailsAsync = null)
    {
        Id = id;
        Title = title;
        CoverUrl = coverUrl;
        Status = status;
        StatusText = GameLibraryStatusMapper.ToDisplayText(status);
        LastSyncedText = lastSyncedText;
        HasWarning = hasWarning;
        WarningText = warningText;
        _launchAsync = launchAsync;
        _openDetailsAsync = openDetailsAsync;
    }

    public string Id { get; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string? CoverUrl { get; set; }

    [ObservableProperty]
    public partial GameLibraryStatus Status { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string? LastSyncedText { get; set; }

    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string? WarningText { get; set; }

    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);

    [RelayCommand]
    private async Task LaunchAsync()
    {
        if (_launchAsync is not null)
        {
            await _launchAsync(Id).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task OpenDetailsAsync()
    {
        if (_openDetailsAsync is not null)
        {
            await _openDetailsAsync(Id).ConfigureAwait(true);
        }
    }

    partial void OnStatusChanged(GameLibraryStatus value) =>
        StatusText = GameLibraryStatusMapper.ToDisplayText(value);
}
