namespace GameSync.Core.Versioning;

/// <summary>
/// Single source of truth for the running app version (semantic MAJOR.MINOR.PATCH).
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// Informational semantic version embedded at build time.
    /// </summary>
    public static string Semantic { get; } =
        typeof(AppVersion).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Select(a => a.InformationalVersion.Split('+')[0].Trim())
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
        ?? typeof(AppVersion).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    public static Version ParseSemantic() =>
        Version.TryParse(NormalizeFourPart(Semantic), out var v) ? v : new Version(0, 0, 0, 0);

    public static bool TryParseTag(string? tagOrVersion, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(tagOrVersion))
        {
            return false;
        }

        var cleaned = tagOrVersion.Trim();
        if (cleaned.StartsWith('v') || cleaned.StartsWith('V'))
        {
            cleaned = cleaned[1..];
        }

        // Drop pre-release suffix: 1.2.3-beta.1
        var dash = cleaned.IndexOf('-');
        if (dash >= 0)
        {
            cleaned = cleaned[..dash];
        }

        if (!Version.TryParse(NormalizeFourPart(cleaned), out var parsed) || parsed is null)
        {
            return false;
        }

        version = parsed;
        return true;
    }

    public static bool IsNewer(Version candidate, Version current) => candidate > current;

    private static string NormalizeFourPart(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            1 => $"{parts[0]}.0.0.0",
            2 => $"{parts[0]}.{parts[1]}.0.0",
            3 => $"{parts[0]}.{parts[1]}.{parts[2]}.0",
            _ => $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}"
        };
    }
}
