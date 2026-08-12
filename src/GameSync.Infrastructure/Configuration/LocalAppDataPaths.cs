using GameSync.Core.Abstractions.Storage;

namespace GameSync.Infrastructure.Configuration;

public sealed class LocalAppDataPaths : ILocalAppDataPaths
{
    public LocalAppDataPaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("LOCALAPPDATA is not available on this machine.");
        }

        Root = Path.Combine(localAppData, "GameSync");
        MachineConfigurationFile = Path.Combine(Root, "machine.json");
        RepositoriesDirectory = Path.Combine(Root, "repositories");
        CacheDirectory = Path.Combine(Root, "cache");
        LogsDirectory = Path.Combine(Root, "logs");
        BackupsDirectory = Path.Combine(Root, "backups");
    }

    public string Root { get; }

    public string MachineConfigurationFile { get; }

    public string RepositoriesDirectory { get; }

    public string CacheDirectory { get; }

    public string LogsDirectory { get; }

    public string BackupsDirectory { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(RepositoriesDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
