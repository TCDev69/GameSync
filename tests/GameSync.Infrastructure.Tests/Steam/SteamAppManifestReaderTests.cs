using GameSync.Infrastructure.Steam;

namespace GameSync.Infrastructure.Tests.Steam;

public sealed class SteamAppManifestReaderTests
{
    [Fact]
    public void ReadManifest_FixtureFile_ExtractsGameInfo()
    {
        var path = Path.Combine("Fixtures", "steam", "appmanifest_570.acf");
        var libraryRoot = Path.GetTempPath();

        var game = SteamAppManifestReader.ReadManifest(path, libraryRoot);

        Assert.NotNull(game);
        Assert.Equal("570", game.AppId);
        Assert.Equal("Dota 2", game.Title);
        Assert.Equal(libraryRoot, game.LibraryRoot);
        Assert.Contains("dota 2 beta", game.InstallDir);
    }

    [Fact]
    public void ReadManifest_MissingFile_ReturnsNull()
    {
        var result = SteamAppManifestReader.ReadManifest("nonexistent.acf", "C:\\");
        Assert.Null(result);
    }
}
