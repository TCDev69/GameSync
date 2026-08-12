using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameSync.App.Navigation;
using GameSync.App.Views;
using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Games;
using GameSync.Core.Abstractions.Metadata;
using GameSync.Core.Abstractions.Steam;
using GameSync.Core.Models;
using GameSync.Core.Services;
using Microsoft.Extensions.Logging;

namespace GameSync.App.ViewModels;

public sealed partial class SteamImportItemViewModel : ObservableObject
{
    public required string AppId { get; init; }
    public required string Title { get; init; }
    public required string InstallDir { get; init; }
    public string? SuggestedMonitorExecutable { get; init; }
    public bool AlreadyInLibrary { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

public sealed partial class SteamImportViewModel : ObservableObject
{
    private readonly ISteamInstalledGamesProvider _steamProvider;
    private readonly IGameMetadataProvider _metadataProvider;
    private readonly ISaveLocationProvider _saveLocationProvider;
    private readonly IGameRegistrationService _registrationService;
    private readonly ISharedGamesConfigurationStore _gamesStore;
    private readonly IMachineConfigurationStore _machineStore;
    private readonly IPathResolver _pathResolver;
    private readonly INavigationService _navigation;
    private readonly ILogger<SteamImportViewModel> _logger;

    private IReadOnlyList<Game> _existingGames = [];

    public SteamImportViewModel(
        ISteamInstalledGamesProvider steamProvider,
        IGameMetadataProvider metadataProvider,
        ISaveLocationProvider saveLocationProvider,
        IGameRegistrationService registrationService,
        ISharedGamesConfigurationStore gamesStore,
        IMachineConfigurationStore machineStore,
        IPathResolver pathResolver,
        INavigationService navigation,
        ILogger<SteamImportViewModel> logger)
    {
        _steamProvider = steamProvider;
        _metadataProvider = metadataProvider;
        _saveLocationProvider = saveLocationProvider;
        _registrationService = registrationService;
        _gamesStore = gamesStore;
        _machineStore = machineStore;
        _pathResolver = pathResolver;
        _navigation = navigation;
        _logger = logger;
    }

    public ObservableCollection<SteamImportItemViewModel> Games { get; } = [];

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial bool IsImporting { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ImportedCount { get; set; }

    [ObservableProperty]
    public partial int SkippedCount { get; set; }

    [ObservableProperty]
    public partial int ErrorCount { get; set; }

    [ObservableProperty]
    public partial bool ShowResults { get; set; }

    [ObservableProperty]
    public partial int NeedsManualSavePathCount { get; set; }

    /// <summary>
    /// When non-null, the UI should show a duplicate dialog for this game.
    /// Call <see cref="ResolveDuplicate"/> with the user's chosen action.
    /// </summary>
    [ObservableProperty]
    public partial SteamImportItemViewModel? PendingDuplicate { get; set; }

    private DuplicateGameAction? _pendingDuplicateAction;
    private readonly SemaphoreSlim _duplicateGate = new(0, 1);

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusText = "Scanning Steam library...";
        Games.Clear();
        ShowResults = false;

        try
        {
            var machine = await _machineStore.LoadAsync().ConfigureAwait(true);
            var repoPath = machine.Repository?.LocalPath;
            if (!string.IsNullOrWhiteSpace(repoPath))
            {
                var config = await _gamesStore.LoadAsync(repoPath).ConfigureAwait(true);
                _existingGames = config.Games.ToList();
            }

            var installed = await _steamProvider.GetInstalledGamesAsync().ConfigureAwait(true);

            foreach (var game in installed)
            {
                var duplicates = _registrationService.FindDuplicateCandidates(
                    _existingGames, game.Title, game.AppId);
                Games.Add(new SteamImportItemViewModel
                {
                    AppId = game.AppId,
                    Title = game.Title,
                    InstallDir = game.InstallDir,
                    SuggestedMonitorExecutable = game.SuggestedMonitorExecutable,
                    AlreadyInLibrary = duplicates.Count > 0,
                    IsSelected = duplicates.Count == 0
                });
            }

            StatusText = $"Found {Games.Count} installed game(s)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Steam scan failed");
            StatusText = "Could not scan Steam library. Is Steam installed?";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var g in Games) g.IsSelected = true;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var g in Games) g.IsSelected = false;
    }

    [RelayCommand]
    private async Task ImportSelectedAsync()
    {
        var selected = Games.Where(g => g.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusText = "Select at least one game to import.";
            return;
        }

        IsImporting = true;
        ImportedCount = 0;
        SkippedCount = 0;
        ErrorCount = 0;
        NeedsManualSavePathCount = 0;
        ShowResults = false;

        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                var item = selected[i];
                StatusText = $"Importing {i + 1}/{selected.Count}: {item.Title}";

                try
                {
                    await ImportSingleGameAsync(item).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to import {Title}", item.Title);
                    ErrorCount++;
                }
            }

            StatusText = $"Done: {ImportedCount} imported, {SkippedCount} skipped, {ErrorCount} error(s), {NeedsManualSavePathCount} need save path setup";
            ShowResults = true;
        }
        finally
        {
            IsImporting = false;
        }
    }

    public void ResolveDuplicate(DuplicateGameAction action)
    {
        _pendingDuplicateAction = action;
        _duplicateGate.Release();
    }

    [RelayCommand]
    private void GoBack() => _navigation.NavigateTo(typeof(LibraryPage));

    private async Task ImportSingleGameAsync(SteamImportItemViewModel item)
    {
        string? coverUrl = null;
        IReadOnlyList<SuggestedSaveLocation> saveSuggestions = [];

        try
        {
            var metadata = await _metadataProvider.GetMetadataAsync(item.AppId).ConfigureAwait(true);
            if (metadata is not null)
            {
                coverUrl = metadata.CoverUrl;
                saveSuggestions = await _saveLocationProvider.SuggestAsync(metadata).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Metadata fetch failed for {AppId}", item.AppId);
        }

        var saves = saveSuggestions.Take(2).Where(s => PathTemplateExists(s.LocalPathTemplate)).Select((s, i) =>
        {
            var id = i == 0 ? "main" : $"slot_{i + 1}";
            var gameId = SaveMapping.SuggestGameId(item.Title);
            return new SaveLocation
            {
                Id = id,
                DisplayName = s.DisplayName,
                LocalPath = s.LocalPathTemplate,
                RemotePath = SaveMapping.BuildDefaultRemotePath(gameId, id),
                Type = SaveLocationType.Directory
            };
        }).ToList();

        if (saves.Count == 0)
        {
            NeedsManualSavePathCount++;
        }

        var request = new GameRegistrationRequest
        {
            Title = item.Title,
            CoverUrl = coverUrl,
            MetadataProviderId = "steam",
            MetadataExternalId = item.AppId,
            SaveLocations = saves,
            Executable = LaunchTarget.BuildSteamRunUri(item.AppId),
            MonitorExecutable = item.SuggestedMonitorExecutable ?? string.Empty
        };

        var duplicates = _registrationService.FindDuplicateCandidates(
            _existingGames, item.Title, item.AppId);

        if (duplicates.Count > 0)
        {
            PendingDuplicate = item;
            _pendingDuplicateAction = null;
            await _duplicateGate.WaitAsync().ConfigureAwait(true);
            PendingDuplicate = null;

            var action = _pendingDuplicateAction ?? DuplicateGameAction.Skip;

            if (action == DuplicateGameAction.UpdateLaunchOnly)
            {
                request = new GameRegistrationRequest
                {
                    Title = request.Title,
                    CoverUrl = request.CoverUrl,
                    MetadataProviderId = request.MetadataProviderId,
                    MetadataExternalId = request.MetadataExternalId,
                    SaveLocations = request.SaveLocations,
                    Executable = request.Executable,
                    MonitorExecutable = request.MonitorExecutable,
                    GameIdOverride = duplicates[0].Id
                };
            }

            var dupResult = await _registrationService.RegisterWithDuplicateActionAsync(
                request, action).ConfigureAwait(true);

            if (dupResult.WasSkipped)
            {
                SkippedCount++;
            }
            else if (dupResult.IsSuccess || dupResult.WasUpdatedOnly)
            {
                ImportedCount++;
            }
            else
            {
                ErrorCount++;
            }

            return;
        }

        var result = await _registrationService.RegisterGameAsync(request).ConfigureAwait(true);
        if (result.IsSuccess)
        {
            ImportedCount++;
        }
        else
        {
            _logger.LogWarning("Registration failed for {Title}: {Error}", item.Title, result.ErrorMessage);
            ErrorCount++;
        }
    }

    private bool PathTemplateExists(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        try
        {
            var resolved = _pathResolver.Resolve(template);
            return Directory.Exists(resolved) || File.Exists(resolved);
        }
        catch
        {
            return false;
        }
    }
}
