using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Git;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;

namespace GameSync.App.ViewModels;

public sealed partial class HistoryEntryItem : ObservableObject
{
    public required string CommitSha { get; init; }
    public required DateTimeOffset CommittedAt { get; init; }
    public required string Message { get; init; }
    public string? AuthorName { get; init; }
    public string? GameId { get; init; }
    public string GameDisplay { get; init; } = "—";
    public string ShortSha => CommitSha.Length > 8 ? CommitSha[..8] : CommitSha;
    public string TimestampText => CommittedAt.ToLocalTime().ToString("g");
}

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IMachineConfigurationStore _machineStore;
    private readonly ISharedGamesConfigurationStore _gamesStore;
    private readonly IGitService _gitService;
    private readonly ISaveService _saveService;
    private readonly IBackupService _backupService;
    private readonly IPathResolver _pathResolver;
    private readonly ILogger<HistoryViewModel> _logger;

    public HistoryViewModel(
        IMachineConfigurationStore machineStore,
        ISharedGamesConfigurationStore gamesStore,
        IGitService gitService,
        ISaveService saveService,
        IBackupService backupService,
        IPathResolver pathResolver,
        ILogger<HistoryViewModel> logger)
    {
        _machineStore = machineStore;
        _gamesStore = gamesStore;
        _gitService = gitService;
        _saveService = saveService;
        _backupService = backupService;
        _pathResolver = pathResolver;
        _logger = logger;
    }

    public ObservableCollection<HistoryEntryItem> Entries { get; } = [];

    [ObservableProperty]
    public partial HistoryEntryItem? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

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

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            Entries.Clear();
            SelectedEntry = null;
            var machine = await _machineStore.LoadAsync().ConfigureAwait(true);
            var repoPath = machine.Repository?.LocalPath;
            if (string.IsNullOrWhiteSpace(repoPath) || !Directory.Exists(repoPath))
            {
                Show("Repository unavailable", "Connect a repository to view save history.", InfoBarSeverity.Warning);
                return;
            }

            var games = await _gamesStore.LoadAsync(repoPath).ConfigureAwait(true);
            var titleById = games.Games.ToDictionary(g => g.Id, g => g.Title, StringComparer.OrdinalIgnoreCase);

            var history = await _gitService.GetHistoryAsync(repoPath, pathFilter: "saves", maxCount: 100).ConfigureAwait(true);
            foreach (var entry in history)
            {
                var gameId = entry.GameId ?? InferGameId(entry.Message, titleById.Keys);
                titleById.TryGetValue(gameId ?? string.Empty, out var title);
                Entries.Add(new HistoryEntryItem
                {
                    CommitSha = entry.CommitSha,
                    CommittedAt = entry.CommittedAt,
                    Message = entry.Message,
                    AuthorName = entry.AuthorName,
                    GameId = gameId,
                    GameDisplay = title ?? gameId ?? "—"
                });
            }

            IsInfoBarOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history");
            Show("Could not load history", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (SelectedEntry is null)
        {
            Show("Select a version", "Choose a commit before restoring.", InfoBarSeverity.Informational);
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedEntry.GameId))
        {
            Show("Unknown game", "This commit could not be matched to a game. Restore was cancelled.", InfoBarSeverity.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var machine = await _machineStore.LoadAsync().ConfigureAwait(true);
            var repoPath = machine.Repository!.LocalPath!;
            var config = await _gamesStore.LoadAsync(repoPath).ConfigureAwait(true);
            var game = config.Games.FirstOrDefault(g => g.Id.Equals(SelectedEntry.GameId, StringComparison.OrdinalIgnoreCase));
            if (game is null)
            {
                Show("Game missing", $"Game '{SelectedEntry.GameId}' is no longer in the library.", InfoBarSeverity.Error);
                return;
            }

            var localPaths = game.SaveLocations
                .Select(s =>
                {
                    try
                    {
                        return _pathResolver.Resolve(s.LocalPath);
                    }
                    catch
                    {
                        return s.LocalPath;
                    }
                })
                .Where(p => !string.IsNullOrWhiteSpace(p) && (File.Exists(p) || Directory.Exists(p)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (localPaths.Length > 0)
            {
                await _backupService.CreateBackupAsync(game.Id, localPaths).ConfigureAwait(true);
            }

            var remotePaths = game.SaveLocations
                .Select(s => s.RemotePath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();
            await _gitService.CheckoutPathsAsync(repoPath, SelectedEntry.CommitSha, remotePaths).ConfigureAwait(true);
            // Always createBackup again inside restore for resolved paths (safety net).
            await _saveService.RestoreRepositoryToLocalAsync(game, repoPath, createBackup: true).ConfigureAwait(true);

            Show(
                "Version restored",
                $"Restored {game.Title} from {SelectedEntry.ShortSha}. A local backup was created first.",
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed");
            Show("Restore failed", ex.Message + " Your previous files should still be in Backups if the backup step succeeded.", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string? InferGameId(string message, IEnumerable<string> knownIds)
    {
        foreach (var id in knownIds)
        {
            if (message.Contains(id, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }

    private void Show(string title, string message, InfoBarSeverity severity)
    {
        InfoBarTitle = title;
        InfoBarMessage = message;
        InfoBarSeverity = severity;
        IsInfoBarOpen = true;
    }
}
