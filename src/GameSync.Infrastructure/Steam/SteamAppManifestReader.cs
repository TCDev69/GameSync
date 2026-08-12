using GameSync.Core.Models;

namespace GameSync.Infrastructure.Steam;

public static class SteamAppManifestReader
{
    public static SteamInstalledGame? ReadManifest(string acfPath, string libraryRoot)
    {
        if (!File.Exists(acfPath))
        {
            return null;
        }

        string content;
        try
        {
            content = File.ReadAllText(acfPath);
        }
        catch
        {
            return null;
        }

        var kv = SteamVdfParser.ParseFlat(content);
        if (!kv.TryGetValue("appid", out var appId) || string.IsNullOrWhiteSpace(appId))
        {
            return null;
        }

        if (!kv.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (!kv.TryGetValue("installdir", out var installDir) || string.IsNullOrWhiteSpace(installDir))
        {
            return null;
        }

        var fullInstallDir = Path.Combine(libraryRoot, "steamapps", "common", installDir);
        var candidates = new List<string>();
        string? suggested = null;

        if (Directory.Exists(fullInstallDir))
        {
            try
            {
                var exes = Directory.GetFiles(fullInstallDir, "*.exe", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .Take(5)
                    .ToList();

                candidates.AddRange(exes);
                suggested = exes.FirstOrDefault();
            }
            catch
            {
                // Permission or path issues
            }
        }

        return new SteamInstalledGame
        {
            AppId = appId.Trim(),
            Title = name.Trim(),
            InstallDir = fullInstallDir,
            LibraryRoot = libraryRoot,
            CandidateExecutables = candidates,
            SuggestedMonitorExecutable = suggested
        };
    }
}
