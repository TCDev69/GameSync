using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Games;
using GameSync.Core.Abstractions.Git;
using GameSync.Core.Models;
using GameSync.Core.Services;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Games;

public sealed class GameRegistrationService : IGameRegistrationService
{
    private readonly IMachineConfigurationStore _machineStore;
    private readonly ISharedGamesConfigurationStore _gamesStore;
    private readonly IPathResolver _pathResolver;
    private readonly IGitService _gitService;
    private readonly ILogger<GameRegistrationService> _logger;

    public GameRegistrationService(
        IMachineConfigurationStore machineStore,
        ISharedGamesConfigurationStore gamesStore,
        IPathResolver pathResolver,
        IGitService gitService,
        ILogger<GameRegistrationService> logger)
    {
        _machineStore = machineStore;
        _gamesStore = gamesStore;
        _pathResolver = pathResolver;
        _gitService = gitService;
        _logger = logger;
    }

    public async Task<GameRegistrationResult> RegisterGameAsync(
        GameRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var machine = await _machineStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var repoPath = machine.Repository?.LocalPath;
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return new GameRegistrationResult
            {
                GameId = string.Empty,
                ErrorMessage = "Connect a repository before adding games."
            };
        }

        var config = await _gamesStore.LoadAsync(repoPath, cancellationToken).ConfigureAwait(false);
        var gameId = request.GameIdOverride ?? SaveMapping.SuggestGameId(request.Title);

        if (config.Games.Any(g => g.Id.Equals(gameId, StringComparison.OrdinalIgnoreCase)))
        {
            return new GameRegistrationResult
            {
                GameId = gameId,
                ErrorMessage = $"A game with id '{gameId}' already exists."
            };
        }

        return await PersistGame(request, gameId, config, machine, repoPath, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GameRegistrationResult> RegisterWithDuplicateActionAsync(
        GameRegistrationRequest request,
        DuplicateGameAction action,
        CancellationToken cancellationToken = default)
    {
        var gameId = request.GameIdOverride ?? SaveMapping.SuggestGameId(request.Title);

        if (action == DuplicateGameAction.Skip)
        {
            return new GameRegistrationResult { GameId = gameId, WasSkipped = true };
        }

        var machine = await _machineStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var repoPath = machine.Repository?.LocalPath;
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return new GameRegistrationResult
            {
                GameId = gameId,
                ErrorMessage = "Connect a repository before adding games."
            };
        }

        var config = await _gamesStore.LoadAsync(repoPath, cancellationToken).ConfigureAwait(false);

        if (action == DuplicateGameAction.UpdateLaunchOnly)
        {
            var existing = config.Games.FirstOrDefault(
                g => g.Id.Equals(gameId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                return new GameRegistrationResult
                {
                    GameId = gameId,
                    ErrorMessage = $"Game '{gameId}' not found for launch update."
                };
            }

            await SaveLaunchConfig(request, existing.Id, machine, cancellationToken).ConfigureAwait(false);
            return new GameRegistrationResult { GameId = existing.Id, WasUpdatedOnly = true };
        }

        // ImportAsNew: generate a unique ID
        if (config.Games.Any(g => g.Id.Equals(gameId, StringComparison.OrdinalIgnoreCase)))
        {
            gameId = $"{gameId}_steam_{request.MetadataExternalId ?? "dup"}";
        }

        return await PersistGame(request, gameId, config, machine, repoPath, cancellationToken)
            .ConfigureAwait(false);
    }

    public IReadOnlyList<Game> FindDuplicateCandidates(
        IReadOnlyList<Game> existingGames,
        string title,
        string? metadataExternalId)
    {
        var suggestedId = SaveMapping.SuggestGameId(title);
        var results = new List<Game>();

        foreach (var g in existingGames)
        {
            if (!string.IsNullOrWhiteSpace(metadataExternalId)
                && string.Equals(g.MetadataExternalId, metadataExternalId, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(g);
                continue;
            }

            if (string.Equals(g.Id, suggestedId, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(g);
                continue;
            }

            if (string.Equals(g.Title, title, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(g);
            }
        }

        return results;
    }

    private async Task<GameRegistrationResult> PersistGame(
        GameRegistrationRequest request,
        string gameId,
        GamesConfiguration config,
        MachineConfiguration machine,
        string repoPath,
        CancellationToken cancellationToken)
    {
        var saves = request.SaveLocations.Select(s => new SaveLocation
        {
            Id = s.Id,
            DisplayName = s.DisplayName,
            LocalPath = s.LocalPath,
            RemotePath = string.IsNullOrWhiteSpace(s.RemotePath)
                ? SaveMapping.BuildDefaultRemotePath(gameId, s.Id)
                : s.RemotePath,
            Type = s.Type
        }).ToList();

        var game = new Game
        {
            Id = gameId,
            Title = request.Title.Trim(),
            CoverUrl = string.IsNullOrWhiteSpace(request.CoverUrl) ? null : request.CoverUrl.Trim(),
            MetadataExternalId = request.MetadataExternalId,
            MetadataProviderId = request.MetadataProviderId,
            SaveLocations = saves
        };

        config.Games.Add(game);
        await _gamesStore.SaveAsync(repoPath, config, cancellationToken).ConfigureAwait(false);

        await SaveLaunchConfig(request, gameId, machine, cancellationToken).ConfigureAwait(false);

        try
        {
            await _gitService.AddAsync(repoPath, ["config/games.json"]).ConfigureAwait(false);
            await _gitService.CommitAsync(
                repoPath,
                SyncCommitMessage.ForLibraryConfiguration(machine.MachineId)).ConfigureAwait(false);
            await _gitService.PushAsync(repoPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Game '{GameId}' saved locally but push failed", gameId);
        }

        return new GameRegistrationResult { GameId = gameId };
    }

    private async Task SaveLaunchConfig(
        GameRegistrationRequest request,
        string gameId,
        MachineConfiguration machine,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Executable))
        {
            return;
        }

        machine.Games[gameId] = new GameLaunchConfiguration
        {
            Executable = request.Executable.Trim(),
            Arguments = request.Arguments,
            WorkingDirectory = request.WorkingDirectory,
            MonitorExecutable = request.MonitorExecutable
        };

        await _machineStore.SaveAsync(machine, cancellationToken).ConfigureAwait(false);
    }
}
