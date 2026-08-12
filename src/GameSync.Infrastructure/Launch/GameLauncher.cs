using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Launch;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Abstractions.Sync;
using GameSync.Core.Errors;
using GameSync.Core.Models;
using GameSync.Core.Services;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Launch;

public sealed class GameLauncher : IGameLauncher, IGameLaunchWorkflow
{
    private static readonly TimeSpan ProcessStartupTimeout = TimeSpan.FromMinutes(3);
    private readonly IMachineConfigurationStore _machineStore;
    private readonly ISharedGamesConfigurationStore _gamesStore;
    private readonly ISyncService _syncService;
    private readonly IProcessLauncher _processLauncher;
    private readonly IProtocolLauncher _protocolLauncher;
    private readonly IGameProcessWatcher _processWatcher;
    private readonly IGameSessionAwaiter _sessionAwaiter;
    private readonly IPathResolver _pathResolver;
    private readonly ILocalAppDataPaths _localPaths;
    private readonly ILogger<GameLauncher> _logger;

    public GameLauncher(
        IMachineConfigurationStore machineStore,
        ISharedGamesConfigurationStore gamesStore,
        ISyncService syncService,
        IProcessLauncher processLauncher,
        IProtocolLauncher protocolLauncher,
        IGameProcessWatcher processWatcher,
        IGameSessionAwaiter sessionAwaiter,
        IPathResolver pathResolver,
        ILocalAppDataPaths localPaths,
        ILogger<GameLauncher> logger)
    {
        _machineStore = machineStore;
        _gamesStore = gamesStore;
        _syncService = syncService;
        _processLauncher = processLauncher;
        _protocolLauncher = protocolLauncher;
        _processWatcher = processWatcher;
        _sessionAwaiter = sessionAwaiter;
        _pathResolver = pathResolver;
        _localPaths = localPaths;
        _logger = logger;
    }

    public async Task<SyncResult> ExecuteAsync(string gameId, CancellationToken cancellationToken = default)
    {
        var result = await LaunchAsync(gameId, progress: null, cancellationToken).ConfigureAwait(false);
        if (result.WasCancelled)
        {
            return SyncResult.Failure(SyncStatus.Failed, result.Message ?? "Launch cancelled.", gameId, result.Error);
        }

        if (!result.Succeeded)
        {
            return result.PreLaunchSync
                   ?? result.PostExitSync
                   ?? SyncResult.Failure(SyncStatus.Failed, result.Message ?? "Launch failed.", gameId, result.Error);
        }

        return result.PostExitSync
               ?? SyncResult.Success(SyncStatus.UpToDate, result.Message ?? "Launch completed.", gameId);
    }

    public async Task<GameLaunchResult> LaunchAsync(
        string gameId,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        Report(progress, LaunchPhase.Preparing, "Preparing", gameId);

        try
        {
            var machine = await _machineStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (machine.Repository?.LocalPath is null)
            {
                throw new RepositoryUnavailableException("No local repository is configured on this machine.");
            }

            var games = await _gamesStore.LoadAsync(machine.Repository.LocalPath, cancellationToken).ConfigureAwait(false);
            var game = games.Games.FirstOrDefault(g => g.Id.Equals(gameId, StringComparison.OrdinalIgnoreCase))
                ?? throw new ConfigurationValidationException([$"Game '{gameId}' was not found in games.json."]);

            if (!machine.Games.TryGetValue(game.Id, out var launchConfig)
                || string.IsNullOrWhiteSpace(launchConfig.Executable))
            {
                throw new GameExecutableNotFoundException(
                    $"No local launch target is configured for game '{game.Id}'.",
                    executablePath: null,
                    gameId: game.Id);
            }

            var launchTarget = launchConfig.Executable.Trim();
            var isProtocol = LaunchTarget.IsProtocolUri(launchTarget);
            string? executable = null;
            string? workingDirectory = null;

            if (isProtocol)
            {
                ValidateProtocolTarget(game.Id, launchTarget);
            }
            else
            {
                executable = _pathResolver.Resolve(launchTarget);
                ValidateExecutable(game.Id, executable, _localPaths);
            }

            if (!string.IsNullOrWhiteSpace(launchConfig.WorkingDirectory))
            {
                workingDirectory = _pathResolver.Resolve(launchConfig.WorkingDirectory);
                if (!Directory.Exists(workingDirectory))
                {
                    throw new GameLaunchFailedException(
                        $"Working directory for game '{game.Id}' is invalid: '{workingDirectory}'.");
                }
            }

            Report(progress, LaunchPhase.CheckingRepository, "Checking repository", game.Id, game.Title);
            Report(progress, LaunchPhase.DownloadingSaves, "Downloading saves", game.Id, game.Title);

            var preSync = await _syncService.SyncBeforeGameLaunchAsync(game.Id, cancellationToken).ConfigureAwait(false);
            if (!preSync.Succeeded)
            {
                Report(progress, LaunchPhase.Error, preSync.Message ?? "Pre-launch sync failed.", game.Id, game.Title, error: preSync.Error, sync: preSync);
                _logger.LogError("Pre-launch sync failed for {GameId}: {Message}", game.Id, preSync.Message);
                return new GameLaunchResult
                {
                    Succeeded = false,
                    GameId = game.Id,
                    GameTitle = game.Title,
                    PreLaunchSync = preSync,
                    Message = preSync.Message ?? "Pre-launch sync failed. Game was not launched.",
                    Error = preSync.Error
                };
            }

            Report(progress, LaunchPhase.RestoringSaves, "Restoring saves", game.Id, game.Title, sync: preSync);
            Report(progress, LaunchPhase.LaunchingGame, "Launching game", game.Id, game.Title);

            int processId;
            int exitCode;
            try
            {
                if (isProtocol)
                {
                    await _protocolLauncher.LaunchAsync(launchTarget, cancellationToken).ConfigureAwait(false);
                    processId = 0;
                    Report(progress, LaunchPhase.GameRunning, "Game launched", game.Id, game.Title);

                    exitCode = await WaitForProtocolSessionEndAsync(
                        game,
                        launchConfig,
                        progress,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await using var process = await _processLauncher.StartAsync(
                        new ProcessStartRequest
                        {
                            ExecutablePath = executable!,
                            Arguments = launchConfig.Arguments,
                            WorkingDirectory = workingDirectory
                        },
                        cancellationToken).ConfigureAwait(false);

                    processId = process.Id;
                    _logger.LogInformation("Game launch started for {GameId} pid={ProcessId}", game.Id, processId);
                    Report(progress, LaunchPhase.GameRunning, "Game running", game.Id, game.Title, processId);

                    try
                    {
                        exitCode = await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning(
                            "Launch wait cancelled for {GameId}; game may still be running (pid={ProcessId})",
                            game.Id,
                            processId);
                        Report(progress, LaunchPhase.Cancelled, "Launch cancelled while the game was running.", game.Id, game.Title, processId);
                        return new GameLaunchResult
                        {
                            Succeeded = false,
                            WasCancelled = true,
                            GameId = game.Id,
                            GameTitle = game.Title,
                            ProcessId = processId,
                            PreLaunchSync = preSync,
                            Message = "GameSync closed or cancelled while the game was running. Post-exit sync was skipped."
                        };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new GameLaunchFailedException($"Failed to start process for game '{game.Id}'.", ex);
            }

            _logger.LogInformation("Game exit for {GameId} exitCode={ExitCode}", game.Id, exitCode);
            Report(progress, LaunchPhase.GameClosed, "Game closed", game.Id, game.Title, processId == 0 ? null : processId);
            Report(progress, LaunchPhase.SavingChanges, "Saving changes", game.Id, game.Title, processId == 0 ? null : processId);
            Report(progress, LaunchPhase.UploadingChanges, "Uploading changes", game.Id, game.Title, processId == 0 ? null : processId);

            var postSync = await _syncService.SyncAfterGameExitAsync(game.Id, cancellationToken).ConfigureAwait(false);
            if (!postSync.Succeeded)
            {
                Report(progress, LaunchPhase.Error, postSync.Message ?? "Post-exit sync failed.", game.Id, game.Title, processId == 0 ? null : processId, postSync.Error, postSync);
                return new GameLaunchResult
                {
                    Succeeded = false,
                    GameId = game.Id,
                    GameTitle = game.Title,
                    ProcessId = processId == 0 ? null : processId,
                    ExitCode = exitCode,
                    PreLaunchSync = preSync,
                    PostExitSync = postSync,
                    Message = postSync.Message ?? "Post-exit sync failed.",
                    Error = postSync.Error
                };
            }

            Report(progress, LaunchPhase.Completed, "Completed", game.Id, game.Title, processId == 0 ? null : processId, sync: postSync);
            return new GameLaunchResult
            {
                Succeeded = true,
                GameId = game.Id,
                GameTitle = game.Title,
                ProcessId = processId == 0 ? null : processId,
                ExitCode = exitCode,
                PreLaunchSync = preSync,
                PostExitSync = postSync,
                Message = "Launch workflow completed."
            };
        }
        catch (OperationCanceledException)
        {
            Report(progress, LaunchPhase.Cancelled, "Launch cancelled.", gameId);
            return new GameLaunchResult
            {
                Succeeded = false,
                WasCancelled = true,
                GameId = gameId,
                Message = "Launch cancelled."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Launch workflow failed for {GameId}", gameId);
            Report(progress, LaunchPhase.Error, ex.Message, gameId, error: ex);
            return new GameLaunchResult
            {
                Succeeded = false,
                GameId = gameId,
                Message = ex.Message,
                Error = ex
            };
        }
    }

    private async Task<int> WaitForProtocolSessionEndAsync(
        Game game,
        GameLaunchConfiguration launchConfig,
        IProgress<LaunchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var monitorPath = ResolveMonitorExecutable(launchConfig);
        if (!string.IsNullOrWhiteSpace(monitorPath) && File.Exists(monitorPath))
        {
            var processName = Path.GetFileNameWithoutExtension(monitorPath);
            return await _processWatcher.WaitForProcessExitByNameAsync(
                processName,
                ProcessStartupTimeout,
                cancellationToken).ConfigureAwait(false);
        }

        Report(
            progress,
            LaunchPhase.AwaitingSessionEnd,
            "Confirm when you have finished playing.",
            game.Id,
            game.Title);

        await _sessionAwaiter.WaitForSessionEndAsync(game.Title, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private string? ResolveMonitorExecutable(GameLaunchConfiguration launchConfig)
    {
        if (string.IsNullOrWhiteSpace(launchConfig.MonitorExecutable))
        {
            return null;
        }

        return _pathResolver.Resolve(launchConfig.MonitorExecutable);
    }

    private static void ValidateProtocolTarget(string gameId, string uri)
    {
        if (!LaunchTarget.IsProtocolUri(uri))
        {
            throw new GameLaunchFailedException($"Launch target for game '{gameId}' is not a supported protocol URI.");
        }
    }

    private static void ValidateExecutable(string gameId, string executable, ILocalAppDataPaths localPaths)
    {
        if (!File.Exists(executable))
        {
            throw GameExecutableNotFoundException.ForGame(gameId, executable);
        }

        var full = Path.GetFullPath(executable);
        var blockedRoots = new[]
        {
            Path.GetFullPath(localPaths.RepositoriesDirectory),
            Path.GetFullPath(localPaths.BackupsDirectory),
            Path.GetFullPath(localPaths.CacheDirectory)
        };

        foreach (var root in blockedRoots)
        {
            if (full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new GameLaunchFailedException(
                    $"Refusing to launch '{full}' because it is inside GameSync-managed storage. Configure a real game executable path.");
            }
        }

        var extension = Path.GetExtension(full);
        if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new GameLaunchFailedException(
                $"Executable for game '{gameId}' must be an .exe file.");
        }
    }

    private static void Report(
        IProgress<LaunchProgress>? progress,
        LaunchPhase phase,
        string message,
        string? gameId = null,
        string? gameTitle = null,
        int? processId = null,
        Exception? error = null,
        SyncResult? sync = null)
    {
        progress?.Report(new LaunchProgress
        {
            Phase = phase,
            Message = message,
            GameId = gameId,
            GameTitle = gameTitle,
            ProcessId = processId,
            Error = error,
            LastSyncResult = sync
        });
    }
}
