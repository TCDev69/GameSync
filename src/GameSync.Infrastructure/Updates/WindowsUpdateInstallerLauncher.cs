using System.Diagnostics;
using GameSync.Core.Abstractions.Updates;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Updates;

/// <summary>
/// Starts the Inno Setup installer via ShellExecute so Windows can raise the elevation prompt
/// (the installer requires admin rights to write into Program Files).
/// </summary>
public sealed class WindowsUpdateInstallerLauncher : IUpdateInstallerLauncher
{
    private readonly ILogger<WindowsUpdateInstallerLauncher> _logger;

    public WindowsUpdateInstallerLauncher(ILogger<WindowsUpdateInstallerLauncher> logger)
    {
        _logger = logger;
    }

    public Task<int> StartAsync(string installerPath, string arguments, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(installerPath) ?? Environment.CurrentDirectory
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Windows refused to start the installer '{installerPath}'.");

        var id = process.Id;
        _logger.LogInformation("Update installer started as process {ProcessId}", id);
        return Task.FromResult(id);
    }
}
