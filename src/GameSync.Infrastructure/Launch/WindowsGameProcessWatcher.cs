using System.Diagnostics;
using GameSync.Core.Abstractions.Launch;
using GameSync.Core.Errors;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Launch;

public sealed class WindowsGameProcessWatcher : IGameProcessWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private readonly ILogger<WindowsGameProcessWatcher> _logger;

    public WindowsGameProcessWatcher(ILogger<WindowsGameProcessWatcher> logger)
    {
        _logger = logger;
    }

    public async Task<int> WaitForProcessExitByNameAsync(
        string processName,
        TimeSpan startupTimeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        cancellationToken.ThrowIfCancellationRequested();

        var name = Path.GetFileNameWithoutExtension(processName.Trim());
        _logger.LogInformation("Waiting for process {ProcessName} to start", name);

        using var startupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startupCts.CancelAfter(startupTimeout);
        Process? process = null;
        try
        {
            while (!startupCts.IsCancellationRequested)
            {
                process = FindNewestProcess(name);
                if (process is not null)
                {
                    break;
                }

                await Task.Delay(PollInterval, startupCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GameLaunchFailedException(
                $"Timed out waiting for '{name}' to start after {startupTimeout.TotalSeconds:0} seconds.");
        }

        if (process is null)
        {
            throw new GameLaunchFailedException($"Process '{name}' did not start.");
        }

        _logger.LogInformation("Monitoring process {ProcessName} pid={ProcessId}", name, process.Id);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return process.ExitCode;
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private static Process? FindNewestProcess(string processName)
    {
        Process? newest = null;
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                if (newest is null || process.StartTime > newest.StartTime)
                {
                    newest?.Dispose();
                    newest = process;
                }
                else
                {
                    process.Dispose();
                }
            }
            catch
            {
                process.Dispose();
            }
        }

        return newest;
    }
}
