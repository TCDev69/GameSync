using System.Diagnostics;
using GameSync.Core.Abstractions.Launch;
using GameSync.Core.Errors;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Launch;

public sealed class WindowsProcessLauncher : IProcessLauncher, IProcessMonitor
{
    private readonly ILogger<WindowsProcessLauncher> _logger;

    public WindowsProcessLauncher(ILogger<WindowsProcessLauncher> logger)
    {
        _logger = logger;
    }

    public Task<ILaunchedProcess> StartAsync(ProcessStartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExecutablePath);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            Arguments = request.Arguments ?? string.Empty,
            UseShellExecute = false,
            WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? Path.GetDirectoryName(request.ExecutablePath) ?? Environment.CurrentDirectory
                : request.WorkingDirectory
        };

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new GameLaunchFailedException($"Process.Start returned null for '{request.ExecutablePath}'.");
        }
        catch (Exception ex) when (ex is not GameLaunchFailedException and not OperationCanceledException)
        {
            throw new GameLaunchFailedException($"Unable to start '{request.ExecutablePath}'.", ex);
        }

        process.EnableRaisingEvents = true;
        _logger.LogInformation("Started process {ProcessId} for {Executable}", process.Id, request.ExecutablePath);
        return Task.FromResult<ILaunchedProcess>(new LaunchedProcess(process, _logger));
    }

    public async Task<int> StartAndWaitAsync(
        string executablePath,
        string arguments,
        string? workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var launched = await StartAsync(
            new ProcessStartRequest
            {
                ExecutablePath = executablePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory
            },
            cancellationToken).ConfigureAwait(false);

        await using (launched.ConfigureAwait(false))
        {
            return await launched.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class LaunchedProcess : ILaunchedProcess
    {
        private readonly Process _process;
        private readonly ILogger _logger;
        private readonly int _id;
        private bool _disposed;

        public LaunchedProcess(Process process, ILogger logger)
        {
            _process = process;
            _logger = logger;
            // Cache immediately — Process.Id throws after exit/dispose ("No process is associated").
            _id = process.Id;
        }

        public int Id => _id;

        public bool HasExited
        {
            get
            {
                if (_disposed)
                {
                    return true;
                }

                try
                {
                    return _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return _process.ExitCode;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Process {ProcessId} exited but ExitCode was unavailable", _id);
                    return -1;
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Process {ProcessId} disappeared while waiting", _id);
                return -1;
            }
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            _process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
