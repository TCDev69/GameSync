using System.Text.Json;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Git;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Abstractions.Repository;
using GameSync.Core.Errors;
using GameSync.Core.GitHub;
using GameSync.Core.Models;
using GameSync.Core.Services;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.GitHub;

public sealed class GitHubRepositoryConnectionService : IGitHubRepositoryConnectionService
{
    private readonly IGitHubService _gitHubService;
    private readonly IRepositoryService _repositoryService;
    private readonly ISharedGamesConfigurationStore _gamesStore;
    private readonly IMachineConfigurationStore _machineStore;
    private readonly IConfigurationValidator _validator;
    private readonly IGitService _gitService;
    private readonly ILogger<GitHubRepositoryConnectionService> _logger;

    public GitHubRepositoryConnectionService(
        IGitHubService gitHubService,
        IRepositoryService repositoryService,
        ISharedGamesConfigurationStore gamesStore,
        IMachineConfigurationStore machineStore,
        IConfigurationValidator validator,
        IGitService gitService,
        ILogger<GitHubRepositoryConnectionService> logger)
    {
        _gitHubService = gitHubService;
        _repositoryService = repositoryService;
        _gamesStore = gamesStore;
        _machineStore = machineStore;
        _validator = validator;
        _gitService = gitService;
        _logger = logger;
    }

    public async Task<RepositoryConnectionResult> ConnectRepositoryAsync(
        RepositoryConfiguration selectedRepository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedRepository);

        try
        {
            GitHubRepositoryValidator.Validate(selectedRepository);
            _logger.LogInformation(
                "Repository selection {Owner}/{Name} branch={Branch}",
                selectedRepository.Owner,
                selectedRepository.Name,
                selectedRepository.DefaultBranch);

            await _gitHubService.VerifyRepositoryAccessAsync(selectedRepository.Owner, selectedRepository.Name, cancellationToken)
                .ConfigureAwait(false);

            var remote = await _gitHubService.GetRepositoryAsync(selectedRepository.Owner, selectedRepository.Name, cancellationToken)
                .ConfigureAwait(false);

            var configuration = selectedRepository with
            {
                Owner = remote.Owner,
                Name = remote.Name,
                CloneUrl = remote.CloneUrl ?? selectedRepository.CloneUrl,
                DefaultBranch = string.IsNullOrWhiteSpace(selectedRepository.DefaultBranch)
                    ? remote.DefaultBranch
                    : selectedRepository.DefaultBranch,
                IsPrivate = remote.IsPrivate,
                LocalPath = string.IsNullOrWhiteSpace(selectedRepository.LocalPath)
                    ? _repositoryService.GetLocalRepositoryPath(remote.Owner, remote.Name)
                    : selectedRepository.LocalPath
            };

            GitHubRepositoryValidator.Validate(configuration);
            _logger.LogInformation("Cloning repository {Owner}/{Name}", configuration.Owner, configuration.Name);

            RepositoryConfiguration ensured;
            try
            {
                ensured = await _repositoryService.EnsureLocalRepositoryAsync(configuration, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is GitHubUnavailableException or RepositoryUnavailableException)
            {
                _logger.LogError(ex, "Clone failed for {Owner}/{Name}", configuration.Owner, configuration.Name);
                return RepositoryConnectionResult.Failure(configuration, ex.Message, ex);
            }

            var localPath = ensured.LocalPath
                ?? throw new RepositoryUnavailableException("Local repository path was not assigned.");

            var (games, initialized) = await EnsureGameSyncStructureAsync(localPath, cancellationToken)
                .ConfigureAwait(false);

            if (initialized)
            {
                try
                {
                    await _gitService.AddAsync(localPath, ["config/games.json"], cancellationToken)
                        .ConfigureAwait(false);
                    await _gitService.CommitAsync(localPath, SyncCommitMessage.ForRepositoryInitialize(), cancellationToken)
                        .ConfigureAwait(false);
                    await _gitService.PushAsync(localPath, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Initialized GameSync structure locally but initial push failed");
                }
            }

            var machine = await _machineStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            var updatedMachine = new MachineConfiguration
            {
                SchemaVersion = machine.SchemaVersion,
                MachineId = machine.MachineId,
                Repository = ensured,
                Games = machine.Games,
                Backup = machine.Backup
            };
            await _machineStore.SaveAsync(updatedMachine, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Connected repository {Owner}/{Name} at {LocalPath} initialized={Initialized}",
                ensured.Owner,
                ensured.Name,
                localPath,
                initialized);

            return RepositoryConnectionResult.Success(
                ensured,
                games,
                initialized,
                initialized
                    ? "Repository connected and GameSync structure initialized."
                    : "Repository connected.");
        }
        catch (Exception ex) when (ex is GameSyncException)
        {
            _logger.LogError(ex, "Repository connection failed");
            return RepositoryConnectionResult.Failure(selectedRepository, ex.Message, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected repository connection failure");
            return RepositoryConnectionResult.Failure(selectedRepository, "Unexpected repository connection failure.", ex);
        }
    }

    private async Task<(GamesConfiguration Games, bool Initialized)> EnsureGameSyncStructureAsync(
        string localPath,
        CancellationToken cancellationToken)
    {
        var configRelative = _gamesStore.GetConfigurationRelativePath().Replace('/', Path.DirectorySeparatorChar);
        var configPath = Path.Combine(localPath, configRelative);
        var configDir = Path.GetDirectoryName(configPath)!;
        var savesDir = Path.Combine(localPath, "saves");

        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(savesDir);

        if (!File.Exists(configPath))
        {
            var created = new GamesConfiguration();
            await _gamesStore.SaveAsync(localPath, created, cancellationToken).ConfigureAwait(false);
            return (created, true);
        }

        try
        {
            var games = await _gamesStore.LoadAsync(localPath, cancellationToken).ConfigureAwait(false);
            var errors = _validator.Validate(games);
            if (errors.Count > 0)
            {
                throw new RepositoryIncompatibleException(
                    "Repository games.json is incompatible with GameSync:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
            }

            return (games, false);
        }
        catch (ConfigurationValidationException ex)
        {
            throw new RepositoryIncompatibleException(
                "Repository games.json is incompatible with GameSync:" + Environment.NewLine + string.Join(Environment.NewLine, ex.Errors),
                ex);
        }
        catch (JsonException ex)
        {
            throw new RepositoryIncompatibleException("Repository games.json is not valid JSON.", ex);
        }
    }
}
