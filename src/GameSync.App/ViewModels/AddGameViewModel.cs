using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameSync.App.Navigation;
using GameSync.App.Views;
using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Git;
using GameSync.Core.Abstractions.Metadata;
using GameSync.Core.Abstractions.Shortcuts;
using GameSync.Core.Models;
using GameSync.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;

namespace GameSync.App.ViewModels;

public sealed partial class SaveLocationEditItem : ObservableObject
{
    [ObservableProperty]
    public partial string Id { get; set; } = "main";

    [ObservableProperty]
    public partial string DisplayName { get; set; } = "Save folder";

    [ObservableProperty]
    public partial string LocalPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RemotePath { get; set; } = string.Empty;

    public bool IsUserEdited { get; set; }
}

public sealed partial class AddGameViewModel : ObservableObject
{
    private readonly IGameMetadataProvider _metadataProvider;
    private readonly ISaveLocationProvider _saveLocationProvider;
    private readonly IMachineConfigurationStore _machineStore;
    private readonly ISharedGamesConfigurationStore _gamesStore;
    private readonly IPathResolver _pathResolver;
    private readonly IGitService _gitService;
    private readonly IShortcutTargetResolver _shortcutResolver;
    private readonly INavigationService _navigation;
    private readonly ILogger<AddGameViewModel> _logger;
    private bool _titleUserEdited;
    private bool _coverUserEdited;
    private bool _suppressEditTracking;

    public AddGameViewModel(
        IGameMetadataProvider metadataProvider,
        ISaveLocationProvider saveLocationProvider,
        IMachineConfigurationStore machineStore,
        ISharedGamesConfigurationStore gamesStore,
        IPathResolver pathResolver,
        IShortcutTargetResolver shortcutResolver,
        IGitService gitService,
        INavigationService navigation,
        ILogger<AddGameViewModel> logger)
    {
        _metadataProvider = metadataProvider;
        _saveLocationProvider = saveLocationProvider;
        _machineStore = machineStore;
        _gamesStore = gamesStore;
        _pathResolver = pathResolver;
        _shortcutResolver = shortcutResolver;
        _gitService = gitService;
        _navigation = navigation;
        _logger = logger;
    }

    public ObservableCollection<GameSearchResult> SearchResults { get; } = [];
    public ObservableCollection<SaveLocationEditItem> SaveLocations { get; } = [];

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CoverUrl { get; set; }

    [ObservableProperty]
    public partial string? MetadataExternalId { get; set; }

    [ObservableProperty]
    public partial string? MetadataProviderId { get; set; }

    [ObservableProperty]
    public partial string Executable { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Arguments { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WorkingDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MonitorExecutable { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanUseSteamLaunch { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsInfoBarOpen { get; set; }

    [ObservableProperty]
    public partial string InfoBarMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity InfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

    partial void OnTitleChanged(string value)
    {
        if (!_suppressEditTracking)
        {
            _titleUserEdited = true;
        }
    }

    partial void OnCoverUrlChanged(string? value)
    {
        if (!_suppressEditTracking)
        {
            _coverUserEdited = true;
        }
    }

    [RelayCommand]
    private async Task FetchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) && string.IsNullOrWhiteSpace(Title))
        {
            ShowError("Enter a game title to search.");
            return;
        }

        IsBusy = true;
        try
        {
            var query = string.IsNullOrWhiteSpace(SearchQuery) ? Title : SearchQuery;
            var results = await _metadataProvider.SearchAsync(query).ConfigureAwait(true);
            SearchResults.Clear();
            foreach (var r in results)
            {
                SearchResults.Add(r);
            }

            if (results.Count == 0)
            {
                ShowError("No matches found. You can still enter details manually.");
                return;
            }

            await ApplyMetadataAsync(results[0]).ConfigureAwait(true);
            IsInfoBarOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata fetch failed");
            ShowError("Could not fetch game metadata. Check your network connection and try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectSearchResultAsync(GameSearchResult? result)
    {
        if (result is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await ApplyMetadataAsync(result).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddSaveLocation()
    {
        var index = SaveLocations.Count + 1;
        var id = index == 1 ? "main" : $"slot_{index}";
        SaveLocations.Add(new SaveLocationEditItem
        {
            Id = id,
            DisplayName = $"Save location {index}",
            LocalPath = string.Empty,
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
    private void UseSteamLaunch()
    {
        if (string.IsNullOrWhiteSpace(MetadataExternalId))
        {
            ShowError("Fetch Steam metadata first or paste a steam:// URI.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(Executable)
            && !LaunchTarget.IsProtocolUri(Executable))
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
        IsInfoBarOpen = false;
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
    private void Cancel() => _navigation.NavigateTo(typeof(LibraryPage));

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ShowError("Game title is required.");
            return;
        }

        if (SaveLocations.Count == 0 || SaveLocations.All(s => string.IsNullOrWhiteSpace(s.LocalPath)))
        {
            ShowError("Add at least one save location path.");
            return;
        }

        IsBusy = true;
        try
        {
            var machine = await _machineStore.LoadAsync().ConfigureAwait(true);
            var repoPath = machine.Repository?.LocalPath;
            if (string.IsNullOrWhiteSpace(repoPath))
            {
                ShowError("Connect a repository before adding games.");
                return;
            }

            var gameId = SaveMapping.SuggestGameId(Title);
            var config = await _gamesStore.LoadAsync(repoPath).ConfigureAwait(true);
            if (config.Games.Any(g => g.Id.Equals(gameId, StringComparison.OrdinalIgnoreCase)))
            {
                ShowError($"A game with id '{gameId}' already exists. Change the title slightly.");
                return;
            }

            var saves = SaveLocations
                .Where(s => !string.IsNullOrWhiteSpace(s.LocalPath))
                .Select(s =>
                {
                    var id = string.IsNullOrWhiteSpace(s.Id) ? "main" : s.Id.Trim();
                    return new SaveLocation
                    {
                        Id = id,
                        DisplayName = string.IsNullOrWhiteSpace(s.DisplayName) ? id : s.DisplayName,
                        LocalPath = _pathResolver.ToPortableTemplate(s.LocalPath.Trim()),
                        RemotePath = string.IsNullOrWhiteSpace(s.RemotePath)
                            ? SaveMapping.BuildDefaultRemotePath(gameId, id)
                            : s.RemotePath.Trim().Replace('\\', '/'),
                        Type = SaveLocationType.Directory
                    };
                })
                .ToList();

            var game = new Game
            {
                Id = gameId,
                Title = Title.Trim(),
                CoverUrl = string.IsNullOrWhiteSpace(CoverUrl) ? null : CoverUrl.Trim(),
                MetadataExternalId = MetadataExternalId,
                MetadataProviderId = MetadataProviderId ?? _metadataProvider.ProviderId,
                SaveLocations = saves
            };

            config.Games.Add(game);
            await _gamesStore.SaveAsync(repoPath, config).ConfigureAwait(true);

            if (!string.IsNullOrWhiteSpace(Executable))
            {
                machine.Games[gameId] = new GameLaunchConfiguration
                {
                    Executable = Executable.Trim(),
                    Arguments = Arguments ?? string.Empty,
                    WorkingDirectory = WorkingDirectory ?? string.Empty,
                    MonitorExecutable = MonitorExecutable ?? string.Empty
                };
                await _machineStore.SaveAsync(machine).ConfigureAwait(true);
            }

            try
            {
                await _gitService.AddAsync(repoPath, ["config/games.json"]).ConfigureAwait(true);
                await _gitService.CommitAsync(
                        repoPath,
                        SyncCommitMessage.ForLibraryConfiguration(machine.MachineId))
                    .ConfigureAwait(true);
                await _gitService.PushAsync(repoPath).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Game saved locally but push of games.json failed");
                ShowError("Game saved on this PC, but publishing to GitHub failed: " + ex.Message);
                _navigation.NavigateTo(typeof(LibraryPage));
                return;
            }

            _navigation.NavigateTo(typeof(LibraryPage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save game");
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyMetadataAsync(GameSearchResult result)
    {
        _suppressEditTracking = true;
        try
        {
            if (!_titleUserEdited)
            {
                Title = result.Title;
            }

            if (!_coverUserEdited)
            {
                CoverUrl = result.CoverUrl;
            }
        }
        finally
        {
            _suppressEditTracking = false;
        }

        MetadataExternalId = result.ExternalId;
        MetadataProviderId = result.ProviderId;
        CanUseSteamLaunch = string.Equals(result.ProviderId, "steam", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(result.ExternalId);

        var metadata = new GameMetadata
        {
            ExternalId = result.ExternalId,
            ProviderId = result.ProviderId,
            Title = result.Title,
            CoverUrl = result.CoverUrl,
            Platform = result.Platform,
            ReleaseDate = result.ReleaseDate
        };

        var suggestions = await _saveLocationProvider.SuggestAsync(metadata).ConfigureAwait(true);
        ApplySuggestions(suggestions);
    }

    private void ApplySuggestions(IReadOnlyList<SuggestedSaveLocation> suggestions)
    {
        if (SaveLocations.Any(s => s.IsUserEdited))
        {
            // Only fill empty non-edited slots; never overwrite user paths.
            foreach (var suggestion in suggestions)
            {
                var empty = SaveLocations.FirstOrDefault(s => !s.IsUserEdited && string.IsNullOrWhiteSpace(s.LocalPath));
                if (empty is null)
                {
                    break;
                }

                empty.LocalPath = suggestion.LocalPathTemplate;
                empty.DisplayName = suggestion.DisplayName;
            }

            return;
        }

        SaveLocations.Clear();
        var index = 0;
        foreach (var suggestion in suggestions.Take(3))
        {
            index++;
            var id = index == 1 ? "main" : $"slot_{index}";
            SaveLocations.Add(new SaveLocationEditItem
            {
                Id = id,
                DisplayName = suggestion.DisplayName,
                LocalPath = suggestion.LocalPathTemplate,
                IsUserEdited = false
            });
        }
    }

    private void ShowError(string message)
    {
        InfoBarMessage = message;
        InfoBarSeverity = InfoBarSeverity.Error;
        IsInfoBarOpen = true;
    }
}
