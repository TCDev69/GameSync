namespace GameSync.Core.Services;

/// <summary>
/// Classifies machine-local launch targets (executable path or protocol URI).
/// </summary>
public static class LaunchTarget
{
    private static readonly HashSet<string> AllowedProtocolSchemes =
        new(StringComparer.OrdinalIgnoreCase) { "steam" };

    public static bool IsProtocolUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return AllowedProtocolSchemes.Contains(uri.Scheme);
    }

    public static bool IsConfigured(string? executable) =>
        !string.IsNullOrWhiteSpace(executable);

    public static string BuildSteamRunUri(string appId) =>
        $"steam://run/{appId.Trim()}";

    public static string BuildSteamRunGameIdUri(string gameId) =>
        $"steam://rungameid/{gameId.Trim()}";

    public static bool TryNormalizeSteamInput(string input, out string uri)
    {
        uri = string.Empty;
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (IsProtocolUri(trimmed))
        {
            uri = trimmed;
            return true;
        }

        if (trimmed.All(char.IsDigit))
        {
            uri = BuildSteamRunUri(trimmed);
            return true;
        }

        return false;
    }
}
