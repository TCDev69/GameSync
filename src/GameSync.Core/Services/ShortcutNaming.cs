namespace GameSync.Core.Services;

public static class ShortcutNaming
{
    public static string SanitizeFileName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var invalid = Path.GetInvalidFileNameChars();
        var chars = displayName.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var sanitized = new string(chars).Trim().TrimEnd('.');
        while (sanitized.Contains("  ", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "Game" : sanitized;
    }

    public static string BuildLaunchArguments(string gameId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        return $"--game {gameId.Trim()}";
    }
}
