using System.Diagnostics;
using GameSync.Core.Abstractions.Launch;
using GameSync.Core.Errors;
using GameSync.Core.Services;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Launch;

public sealed class WindowsProtocolLauncher : IProtocolLauncher
{
    private readonly ILogger<WindowsProtocolLauncher> _logger;

    public WindowsProtocolLauncher(ILogger<WindowsProtocolLauncher> logger)
    {
        _logger = logger;
    }

    public Task LaunchAsync(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        cancellationToken.ThrowIfCancellationRequested();

        if (!LaunchTarget.IsProtocolUri(uri))
        {
            throw new GameLaunchFailedException($"Unsupported protocol launch target: '{uri}'.");
        }

        try
        {
            var started = Process.Start(new ProcessStartInfo
            {
                FileName = uri.Trim(),
                UseShellExecute = true
            });

            if (started is null)
            {
                throw new GameLaunchFailedException($"Process.Start returned null for '{uri}'.");
            }
        }
        catch (Exception ex) when (ex is not GameLaunchFailedException and not OperationCanceledException)
        {
            throw new GameLaunchFailedException($"Unable to launch '{uri}'.", ex);
        }

        _logger.LogInformation("Launched protocol URI {Uri}", uri);
        return Task.CompletedTask;
    }
}
