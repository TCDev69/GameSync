using GameSync.Core.Abstractions.Steam;
using GameSync.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GameSync.Infrastructure.Steam;

public sealed class WindowsSteamInstalledGamesProvider : ISteamInstalledGamesProvider
{
    private readonly ILogger<WindowsSteamInstalledGamesProvider> _logger;

    public WindowsSteamInstalledGamesProvider(ILogger<WindowsSteamInstalledGamesProvider> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<SteamInstalledGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var steamRoot = ResolveSteamRoot();
        if (steamRoot is null)
        {
            _logger.LogWarning("Steam installation not found in registry");
            return Task.FromResult<IReadOnlyList<SteamInstalledGame>>([]);
        }

        _logger.LogInformation("Steam root: {SteamRoot}", steamRoot);

        var libraryPaths = ResolveLibraryPaths(steamRoot);
        _logger.LogInformation("Found {Count} Steam library folder(s)", libraryPaths.Count);

        var games = new List<SteamInstalledGame>();
        var seenAppIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var libraryPath in libraryPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var steamAppsDir = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamAppsDir))
            {
                continue;
            }

            string[] manifests;
            try
            {
                manifests = Directory.GetFiles(steamAppsDir, "appmanifest_*.acf");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not enumerate manifests in {Dir}", steamAppsDir);
                continue;
            }

            foreach (var manifestPath in manifests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var game = SteamAppManifestReader.ReadManifest(manifestPath, libraryPath);
                if (game is not null && seenAppIds.Add(game.AppId))
                {
                    games.Add(game);
                }
            }
        }

        games.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
        return Task.FromResult<IReadOnlyList<SteamInstalledGame>>(games);
    }

    private static string? ResolveSteamRoot()
    {
        var path = ReadRegistryString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath")
                   ?? ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        path = path.Replace('/', '\\');
        return Directory.Exists(path) ? path : null;
    }

    private static string? ReadRegistryString(RegistryKey root, string subKeyPath, string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath);
            return key?.GetValue(valueName) as string;
        }
        catch
        {
            return null;
        }
    }

    private IReadOnlyList<string> ResolveLibraryPaths(string steamRoot)
    {
        var results = new List<string> { steamRoot };
        var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath))
        {
            _logger.LogDebug("libraryfolders.vdf not found at {Path}", vdfPath);
            return results;
        }

        try
        {
            var content = File.ReadAllText(vdfPath);
            var parsed = SteamVdfParser.ParseLibraryFolderPaths(content);
            foreach (var p in parsed)
            {
                var normalized = p.Replace('/', '\\');
                if (Directory.Exists(normalized) && !results.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(normalized);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse libraryfolders.vdf");
        }

        return results;
    }
}
