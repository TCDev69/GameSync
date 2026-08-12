using GameSync.Infrastructure.Steam;

namespace GameSync.Infrastructure.Tests.Steam;

public sealed class SteamVdfParserTests
{
    [Fact]
    public void ParseFlat_ExtractsTopLevelKeyValuePairs()
    {
        var content = """
            "AppState"
            {
                "appid"		"570"
                "name"		"Dota 2"
                "installdir"		"dota 2 beta"
            }
            """;

        var result = SteamVdfParser.ParseFlat(content);

        Assert.Equal("570", result["appid"]);
        Assert.Equal("Dota 2", result["name"]);
        Assert.Equal("dota 2 beta", result["installdir"]);
    }

    [Fact]
    public void ParseFlat_SkipsNestedBlocks()
    {
        var content = """
            "AppState"
            {
                "appid"		"570"
                "InstalledDepots"
                {
                    "571"
                    {
                        "manifest"		"123"
                    }
                }
                "name"		"Dota 2"
            }
            """;

        var result = SteamVdfParser.ParseFlat(content);

        Assert.Equal("570", result["appid"]);
        Assert.Equal("Dota 2", result["name"]);
        Assert.False(result.ContainsKey("manifest"));
    }

    [Fact]
    public void ParseLibraryFolderPaths_ReturnsAllPaths()
    {
        var content = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                    "label"		""
                    "apps"
                    {
                        "570"		"12345"
                    }
                }
                "1"
                {
                    "path"		"D:\\SteamLibrary"
                    "apps"
                    {
                        "1091500"		"67890"
                    }
                }
            }
            """;

        var paths = SteamVdfParser.ParseLibraryFolderPaths(content);

        Assert.Equal(2, paths.Count);
        Assert.Contains(paths, p => p.Contains("Steam"));
        Assert.Contains(paths, p => p.Contains("SteamLibrary"));
    }

    [Fact]
    public void ParseFlat_EmptyContent_ReturnsEmpty()
    {
        var result = SteamVdfParser.ParseFlat("");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseLibraryFolderPaths_EmptyContent_ReturnsEmpty()
    {
        var result = SteamVdfParser.ParseLibraryFolderPaths("");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseFlat_FixtureFile()
    {
        var path = Path.Combine("Fixtures", "steam", "appmanifest_570.acf");
        var content = File.ReadAllText(path);

        var result = SteamVdfParser.ParseFlat(content);

        Assert.Equal("570", result["appid"]);
        Assert.Equal("Dota 2", result["name"]);
        Assert.Equal("dota 2 beta", result["installdir"]);
    }

    [Fact]
    public void ParseLibraryFolderPaths_FixtureFile()
    {
        var path = Path.Combine("Fixtures", "steam", "libraryfolders.vdf");
        var content = File.ReadAllText(path);

        var paths = SteamVdfParser.ParseLibraryFolderPaths(content);

        Assert.Equal(2, paths.Count);
        Assert.Contains(paths, p => p.Contains("Steam", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(paths, p => p.Contains("SteamLibrary", StringComparison.OrdinalIgnoreCase));
    }
}
