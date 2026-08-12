using System.Text.RegularExpressions;
using GameSync.Core.Abstractions;
using GameSync.Core.Errors;

namespace GameSync.Infrastructure.Paths;

/// <summary>
/// Expands Windows environment variables and enforces repository path containment.
/// Also registered conceptually as PathResolver for the sync engine.
/// </summary>
public sealed class PathResolver : IPathResolver
{
    private static readonly Regex EnvVarRegex = new("%([^%]+)%", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> KnownVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "USERPROFILE",
        "APPDATA",
        "LOCALAPPDATA",
        "PROGRAMFILES",
        "PROGRAMFILES(X86)",
        "PROGRAMDATA",
        "TEMP",
        "TMP",
        "SYSTEMROOT",
        "WINDIR",
        "HOMEDRIVE",
        "HOMEPATH",
        "PUBLIC",
        "USERNAME"
    };

    private readonly Func<string, string?> _getEnvironmentVariable;

    public PathResolver()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    public PathResolver(Func<string, string?> getEnvironmentVariable)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
    }

    public string Resolve(string pathTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathTemplate);

        var expanded = EnvVarRegex.Replace(pathTemplate, match =>
        {
            var name = match.Groups[1].Value;
            var value = _getEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? match.Value : value;
        });

        return Normalize(expanded);
    }

    public bool IsValidTemplate(string pathTemplate)
    {
        if (string.IsNullOrWhiteSpace(pathTemplate))
        {
            return false;
        }

        foreach (Match match in EnvVarRegex.Matches(pathTemplate))
        {
            var name = match.Groups[1].Value;
            if (!KnownVariables.Contains(name) && string.IsNullOrEmpty(_getEnvironmentVariable(name)))
            {
                return false;
            }
        }

        return true;
    }

    public string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var unified = path.Replace('/', Path.DirectorySeparatorChar).Trim();
        return Path.GetFullPath(unified);
    }

    public bool IsSafeRemotePath(string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            return false;
        }

        var trimmed = remotePath.Replace('\\', '/').Trim();
        if (trimmed.StartsWith('/') || trimmed.Contains(':') || trimmed.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        if (Path.IsPathRooted(remotePath))
        {
            return false;
        }

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && segments.All(s => s is not "." and not "..");
    }

    public string MapRemotePathToRepository(string repositoryRoot, string remotePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);

        if (!IsSafeRemotePath(remotePath))
        {
            throw new PathTraversalException($"Remote path '{remotePath}' is not allowed.", remotePath);
        }

        var root = Normalize(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var combined = Normalize(Path.Combine(root, remotePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!IsPathUnderRoot(root, combined))
        {
            throw new PathTraversalException($"Remote path '{remotePath}' escapes the repository root.", remotePath);
        }

        return combined;
    }

    public string GetRepositoryRelativePath(string repositoryRoot, string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        var root = Normalize(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Normalize(absolutePath);

        if (!IsPathUnderRoot(root, full))
        {
            throw new PathTraversalException($"Path '{absolutePath}' is outside the repository.", absolutePath);
        }

        var relative = Path.GetRelativePath(root, full);
        return relative.Replace('\\', '/');
    }

    public bool IsAllowedLocalSaveTarget(string resolvedAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(resolvedAbsolutePath))
        {
            return false;
        }

        string full;
        try
        {
            full = Normalize(resolvedAbsolutePath);
        }
        catch
        {
            return false;
        }

        var markers = new[]
        {
            Path.Combine("GameSync", "repositories") + Path.DirectorySeparatorChar,
            Path.Combine("GameSync", "backups") + Path.DirectorySeparatorChar,
            Path.Combine("GameSync", "cache") + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + ".ssh" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + ".gnupg" + Path.DirectorySeparatorChar,
        };

        foreach (var marker in markers)
        {
            if (full.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Reject Windows / system roots as save destinations.
        var windows = _getEnvironmentVariable("WINDIR") ?? _getEnvironmentVariable("SYSTEMROOT");
        if (!string.IsNullOrWhiteSpace(windows))
        {
            var winRoot = Normalize(windows).TrimEnd(Path.DirectorySeparatorChar);
            if (IsPathUnderRoot(winRoot, full))
            {
                return false;
            }
        }

        return true;
    }

    public string ToPortableTemplate(string absoluteOrTemplatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteOrTemplatePath);
        var trimmed = absoluteOrTemplatePath.Trim();

        // Already uses env tokens — keep them, normalize separators.
        if (trimmed.Contains('%', StringComparison.Ordinal))
        {
            return trimmed.Replace('\\', '/');
        }

        var full = Normalize(trimmed).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var roots = new List<(string Name, string Path)>();
        foreach (var name in PortableRootVariables)
        {
            var value = _getEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            try
            {
                var root = Normalize(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                roots.Add((name, root));
            }
            catch
            {
                // Skip unusable env values.
            }
        }

        foreach (var (name, root) in roots.OrderByDescending(r => r.Path.Length))
        {
            if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                return $"%{name}%";
            }

            var prefix = root + Path.DirectorySeparatorChar;
            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var relative = full[prefix.Length..].Replace('\\', '/');
                return $"%{name}%/{relative}";
            }
        }

        return full.Replace('\\', '/');
    }

    private static readonly string[] PortableRootVariables =
    [
        "LOCALAPPDATA",
        "APPDATA",
        "USERPROFILE",
        "PROGRAMFILES(X86)",
        "PROGRAMFILES",
        "PROGRAMDATA",
        "PUBLIC",
        "TEMP",
        "TMP"
    ];

    private static bool IsPathUnderRoot(string root, string candidate)
    {
        var rootPrefix = root + Path.DirectorySeparatorChar;
        return candidate.Equals(root, StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Backward-compatible alias used by earlier foundation tests/registration.
/// </summary>
public sealed class WindowsPathResolver : IPathResolver
{
    private readonly PathResolver _inner;

    public WindowsPathResolver()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    public WindowsPathResolver(Func<string, string?> getEnvironmentVariable)
    {
        _inner = new PathResolver(getEnvironmentVariable);
    }

    public string Resolve(string pathTemplate) => _inner.Resolve(pathTemplate);

    public bool IsValidTemplate(string pathTemplate) => _inner.IsValidTemplate(pathTemplate);

    public string Normalize(string path) => _inner.Normalize(path);

    public bool IsSafeRemotePath(string remotePath) => _inner.IsSafeRemotePath(remotePath);

    public string MapRemotePathToRepository(string repositoryRoot, string remotePath) =>
        _inner.MapRemotePathToRepository(repositoryRoot, remotePath);

    public string GetRepositoryRelativePath(string repositoryRoot, string absolutePath) =>
        _inner.GetRepositoryRelativePath(repositoryRoot, absolutePath);

    public bool IsAllowedLocalSaveTarget(string resolvedAbsolutePath) =>
        _inner.IsAllowedLocalSaveTarget(resolvedAbsolutePath);

    public string ToPortableTemplate(string absoluteOrTemplatePath) =>
        _inner.ToPortableTemplate(absoluteOrTemplatePath);
}
