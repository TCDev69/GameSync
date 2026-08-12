using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameSync.App.Navigation;
using GameSync.App.Views;
using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Git;
using GameSync.Core.Abstractions.Shortcuts;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Models;
using GameSync.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;

namespace GameSync.App.ViewModels;

public sealed partial class GameDetailsViewModel : ObservableObject
{
    private readonly IMachineConfigurationStore _machineStore;
    private readonly ISharedGamesConfigurationStore _gamesStore;
    private readonly IPathResolver _pathResolver;
    private readonly IGitService _gitService;
    private readonly IShortcutService _shortcutService;
    private readonly IShortcutTargetResolver _shortcutResolver;
    private readonly ISyncWorkflow _syncWorkflow;
    private readonly INavigationService _navigation;
    private readonly ILogger<GameDetailsViewModel> _logger;
    private string _gameId = string.Empty;

    public GameDetailsViewModel(
        IMachineConfigurationStore machineStore,
        ISharedGamesConfigurationStore gamesStore,
        IPathResolver pathResolver,
        IGitService gitService,
        IShortcutService shortcutService,
        IShortcutTargetResolver shortcutResolver,
        ISyncWorkflow syncWorkflow,
        INavigationService navigation,
        ILogger<GameDetailsViewModel> logger)
    {
        _machineStore = machineStore;
        _gamesStore = gamesStore;
        _pathResolver = pathResolver;
        _gitService = gitService;
        _shortcutService = shortcutService;
        _shortcutResolver = shortcutResolver;
        _syncWorkflow = syncWorkflow;
        _navigation = navigation;
        _logger = logger;
    }

    public ObservableCollection<SaveLocationEditItem> SaveLocations { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CoverUrl { get; set; }

    [ObservableProperty]
    public partial string Executable { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Arguments { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WorkingDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MonitorExecutable { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? MetadataExternalId { get; set; }

    [ObservableProperty]
    public partial bool CanUseSteamLaunch { get; set; }

    [ObservableProperty]
    public partial bool HasDesktopShortcut { get; set; }

    [ObservableProperty]
    public partial bool HasStartMenuShortcut { get; set; }

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

    public async Task LoadAsync(string gameId)
    {
        _gameId = gameId;
        IsBusy = true;
        try
        {
            var machine = await _machineStore.LoadAsync().ConfigureAwait(true);
            var repoPath = machine.Repository?.LocalPath
                ?? throw new InvalidOperationException("Repository is not connected.");
            var config = await _gamesStore.LoadAsync(repoPath).ConfigureAwait(true);
            var game = config.Games.FirstOrDefault(g => g.Id.Equals(gameId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Game '{gameId}' was not found.");

            Title = game.Title;
            CoverUrl = game.CoverUrl;
            MetadataExternalId = game.MetadataExternalId;
            CanUseSteamLaunch = string.Equals(game.MetadataProviderId, "steam", StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrWhiteSpace(game.MetadataExternalId);
            SaveLocations.Clear();
            foreach (var s in game.SaveLocations)
            {
                SaveLocations.Add(new SaveLocationEditItem
                {
                    Id = s.Id,
                    DisplayName = s.DisplayName ?? s.Id,
                    LocalPath = s.LocalPath,
                    RemotePath = s.RemotePath,
                    IsUserEdited = true
                });
            }

            if (machine.Games.TryGetValue(gameId, out var launch))
            {
                Executable = launch.Executable;
                Arguments = launch.Arguments;
                WorkingDirectory = launch.WorkingDirectory;
                MonitorExecutable = launch.MonitorExecutable;
            }
            else
            {
                Executable = string.Empty;
                Arguments = string.Empty;
                WorkingDirectory = string.Empty;
                MonitorExecutable = string.Empty;
            }

            HasDesktopShortcut = await _shortcutService.ExistsAsync(DesktopShortcut()).ConfigureAwait(true);
            HasStartMenuShortcut = await _shortcutService.ExistsAsync(StartMenuShortcut()).ConfigureAwait(true);
            IsInfoBarOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load game details");
            Show("Could not load game", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void UseSteamLaunch()
    {
        if (string.IsNullOrWhiteSpace(MetadataExternalId))
        {
            Show("Steam launch unavailable", "This game has no Steam app id.", InfoBarSeverity.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(Executable) && !LaunchTarget.IsProtocolUri(Executable))
        {
            try
            {
                var resolved = _pathResolver.Resolve(Executable);
                if (File.Exists(resolved))
                {
                    MonitorExecutable = Executable;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not resolve executable for Steam monitor path");
            }
        }

        Executable = LaunchTarget.BuildSteamRunUri(MetadataExternalId);
    }

    public void ApplyLaunchPath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return;
        }

        var trimmed = rawPath.Trim();
        if (LaunchTarget.TryNormalizeSteamInput(trimmed, out var steamUri))
        {
            Executable = steamUri;
            return;
        }

        if (LaunchTarget.IsProtocolUri(trimmed))
        {
            Executable = trimmed;
            return;
        }

        var resolved = _shortcutResolver.TryResolveTargetPath(trimmed) ?? trimmed;
        Executable = resolved;
        MonitorExecutable = string.Empty;
        if (string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            WorkingDirectory = Path.GetDirectoryName(resolved) ?? string.Empty;
        }
    }

    [RelayCommand]
    private void AddSaveLocation()
    {
        SaveLocations.Add(new SaveLocationEditItem
        {
            Id = $"slot_{SaveLocations.Count + 1}",
            DisplayName = "Save location",
            IsUserEdited = true
        });
    }

    [RelayCommand]
    private void RemoveSaveLocation(SaveLocationEditItem? item)
    {
        if (item is not null)
        {
            SaveLocations.Remove(item);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_gameId))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var machine = await _machineStore.LoadAsync().ConfigureAwait(true);
            var repoPath = machine.Repository?.LocalPath
                ?? throw new InvalidOperationException("Repository is not connected.");
            var config = await _gamesStore.LoadAsync(repoPath).ConfigureAwait(true);
            var index = config.Games.ToList().FindIndex(g => g.Id.Equals(_gameId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                throw new InvalidOperationException("Game was not found.");
            }

            var existing = config.Games[index];
            var saves = SaveLocations
                .Where(s => !string.IsNullOrWhiteSpace(s.LocalPath))
                .Select(s => new SaveLocation
                {
                    Id = string.IsNullOrWhiteSpace(s.Id) ? "main" : s.Id.Trim(),
                    DisplayName = s.DisplayName,
                    LocalPath = _pathResolver.ToPortableTemplate(s.LocalPath.Trim()),
                    RemotePath = string.IsNullOrWhiteSpace(s.RemotePath)
                        ? SaveMapping.BuildDefaultRemotePath(_gameId, string.IsNullOrWhiteSpace(s.Id) ? "main" : s.Id)
                        : s.RemotePath.Trim().Replace('\\', '/'),
                    Type = SaveLocationType.Directory
                })
                .ToList();

            config.Games[index] = new Game
            {
                Id = existing.Id,
                Title = Title.Trim(),
                CoverUrl = string.IsNullOrWhiteSpace(CoverUrl) ? null : CoverUrl.Trim(),
                MetadataExternalId = existing.MetadataExternalId,
                MetadataProviderId = existing.MetadataProviderId,
                Platform = existing.Platform,
                ReleaseDate = existing.ReleaseDate,
                SaveLocations = saves
            };
            await _gamesStore.SaveAsync(repoPath, config).ConfigureAwait(true);

            if (!string.IsNullOrWhiteSpace(Executable))
            {
                machine.Games[_gameId] = new GameLaunchConfiguration
                {
                    Executable = Executable.Trim(),
                    Arguments = Arguments ?? string.Empty,
                    WorkingDirectory = WorkingDirectory ?? string.Empty,
                    MonitorExecutable = MonitorExecutable ?? string.Empty
                };
            }
            else
            {
                machine.Games.Remove(_gameId);
            }

            await _machineStore.SaveAsync(machine).ConfigureAwait(true);

            try
            {
                await _gitService.AddAsync(repoPath, ["config/games.json"]).ConfigureAwait(true);
                await _gitService.CommitAsync(
                        repoPath,
                        SyncCommitMessage.ForLibraryConfiguration(machine.MachineId))
                    .ConfigureAwait(true);
                await _gitService.PushAsync(repoPath).ConfigureAwait(true);
                Show("Saved", "Shared and This PC settings were updated and published.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Game details saved locally but push failed");
                Show("Saved locally", "Settings saved on this PC, but publishing to GitHub failed: " + ex.Message, InfoBarSeverity.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save game details");
            Show("Save failed", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (string.IsNullOrWhiteSpace(_gameId))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _syncWorkflow.SyncGameAsync(_gameId).ConfigureAwait(true);
            if (!result.Succeeded && result.Conflicts.Count > 0)
            {
                ConflictDetected?.Invoke(this, result);
                return;
            }

            Show(
                result.Succeeded ? "Sync completed" : "Sync failed",
                result.Message ?? (result.Succeeded ? "OK" : "Synchronization failed."),
                result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            Show("Sync failed", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event EventHandler<SyncResult>? ConflictDetected;

    [RelayCommand]
    private async Task ToggleDesktopShortcutAsync()
    {
        var config = DesktopShortcut();
        if (HasDesktopShortcut)
        {
            await _shortcutService.DeleteAsync(config).ConfigureAwait(true);
            HasDesktopShortcut = false;
        }
        else
        {
            await _shortcutService.CreateAsync(config).ConfigureAwait(true);
            HasDesktopShortcut = true;
        }
    }

    [RelayCommand]
    private async Task ToggleStartMenuShortcutAsync()
    {
        var config = StartMenuShortcut();
        if (HasStartMenuShortcut)
        {
            await _shortcutService.DeleteAsync(config).ConfigureAwait(true);
            HasStartMenuShortcut = false;
        }
        else
        {
            await _shortcutService.CreateAsync(config).ConfigureAwait(true);
            HasStartMenuShortcut = true;
        }
    }

    [RelayCommand]
    private void Back() => _navigation.NavigateTo(typeof(LibraryPage));

    private ShortcutConfiguration DesktopShortcut() => new()
    {
        GameId = _gameId,
        DisplayName = Title,
        Kind = ShortcutKind.Desktop,
        Description = $"Launch {Title} via GameSync",
        IconPath = ResolveGameIconPath()
    };

    private ShortcutConfiguration StartMenuShortcut() => new()
    {
        GameId = _gameId,
        DisplayName = Title,
        Kind = ShortcutKind.StartMenu,
        Description = $"Launch {Title} via GameSync",
        IconPath = ResolveGameIconPath()
    };

    private string? ResolveGameIconPath()
    {
        if (string.IsNullOrWhiteSpace(Executable) || LaunchTarget.IsProtocolUri(Executable))
        {
            return null;
        }

        try
        {
            var path = _pathResolver.Resolve(Executable.Trim());
            return File.Exists(path) ? path : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve executable icon for shortcut");
            return null;
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
