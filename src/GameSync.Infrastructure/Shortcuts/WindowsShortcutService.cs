using System.Runtime.InteropServices;
using GameSync.Core.Abstractions.Shortcuts;
using GameSync.Core.Models;
using GameSync.Core.Services;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Shortcuts;

public sealed class WindowsShortcutService : IShortcutService
{
    private readonly ILogger<WindowsShortcutService> _logger;

    public WindowsShortcutService(ILogger<WindowsShortcutService> logger)
    {
        _logger = logger;
    }

    public string BuildLaunchArguments(string gameId) => ShortcutNaming.BuildLaunchArguments(gameId);

    public string GetShortcutPath(ShortcutConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var fileName = ShortcutNaming.SanitizeFileName(configuration.DisplayName) + ".lnk";
        return configuration.Kind switch
        {
            ShortcutKind.Desktop => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                fileName),
            ShortcutKind.StartMenu => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs",
                "GameSync",
                fileName),
            _ => throw new ArgumentOutOfRangeException(nameof(configuration), configuration.Kind, "Unknown shortcut kind.")
        };
    }

    public Task CreateAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetShortcutPath(configuration);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var target = ResolveGameSyncExecutable();
        var arguments = BuildLaunchArguments(configuration.GameId);
        var workingDirectory = Path.GetDirectoryName(target) ?? string.Empty;

        CreateShortcut(path, target, arguments, configuration.Description ?? $"Launch {configuration.DisplayName} via GameSync", configuration.IconPath, workingDirectory);
        _logger.LogInformation("Created {Kind} shortcut for {GameId} at {Path} -> {Target}", configuration.Kind, configuration.GameId, path, target);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetShortcutPath(configuration);
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInformation("Removed shortcut {Path}", path);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(ShortcutConfiguration configuration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(GetShortcutPath(configuration)));
    }

    /// <summary>
    /// Resolve the installed/running GameSync.exe (unpackaged Inno install or local publish).
    /// </summary>
    private static string ResolveGameSyncExecutable()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            return processPath;
        }

        var entry = Environment.GetCommandLineArgs().FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(entry) && File.Exists(entry))
        {
            return Path.GetFullPath(entry);
        }

        throw new InvalidOperationException("Unable to resolve GameSync.exe path for shortcut creation.");
    }

    private static void CreateShortcut(
        string shortcutPath,
        string targetPath,
        string arguments,
        string description,
        string? iconPath,
        string workingDirectory)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell COM component is unavailable.");

        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Failed to create WScript.Shell.");
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath]);

            SetComProperty(shortcut, "TargetPath", targetPath);
            SetComProperty(shortcut, "Arguments", arguments);
            SetComProperty(shortcut, "WorkingDirectory", workingDirectory);
            SetComProperty(shortcut, "Description", description);
            SetComProperty(shortcut, "WindowStyle", 1);
            // IconLocation needs "path,index" — bare .exe paths alone often fall back to the target icon.
            if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
            {
                SetComProperty(shortcut, "IconLocation", FormatIconLocation(iconPath));
            }
            else if (File.Exists(targetPath))
            {
                SetComProperty(shortcut, "IconLocation", FormatIconLocation(targetPath));
            }

            shortcut!.GetType().InvokeMember(
                "Save",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shortcut,
                null);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    private static string FormatIconLocation(string path) =>
        path.Contains(',', StringComparison.Ordinal) ? path : path + ",0";

    private static void SetComProperty(object? target, string name, object value)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.GetType().InvokeMember(
            name,
            System.Reflection.BindingFlags.SetProperty,
            null,
            target,
            [value]);
    }
}
