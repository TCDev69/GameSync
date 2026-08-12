using System.Runtime.InteropServices;
using GameSync.Core.Abstractions.Shortcuts;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Shortcuts;

/// <summary>
/// Resolves Windows .lnk shortcut targets to executable paths.
/// </summary>
public sealed class WindowsShortcutTargetResolver : IShortcutTargetResolver
{
    private readonly ILogger<WindowsShortcutTargetResolver> _logger;

    public WindowsShortcutTargetResolver(ILogger<WindowsShortcutTargetResolver> logger)
    {
        _logger = logger;
    }

    public string? TryResolveTargetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(path);
        }

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            object? shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return null;
            }

            try
            {
                var shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    shell,
                    [path]);

                if (shortcut is null)
                {
                    return null;
                }

                try
                {
                    var target = shortcut.GetType().InvokeMember(
                        "TargetPath",
                        System.Reflection.BindingFlags.GetProperty,
                        null,
                        shortcut,
                        null) as string;

                    return string.IsNullOrWhiteSpace(target) ? null : Path.GetFullPath(target);
                }
                finally
                {
                    if (Marshal.IsComObject(shortcut))
                    {
                        Marshal.FinalReleaseComObject(shortcut);
                    }
                }
            }
            finally
            {
                if (Marshal.IsComObject(shell))
                {
                    Marshal.FinalReleaseComObject(shell);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve shortcut {Path}", path);
            return null;
        }
    }
}
