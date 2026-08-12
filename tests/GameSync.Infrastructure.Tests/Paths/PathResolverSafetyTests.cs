using FluentAssertions;
using GameSync.Core.Services;
using GameSync.Infrastructure.Paths;

namespace GameSync.Infrastructure.Tests.Paths;

public sealed class PathResolverSafetyTests
{
    private readonly PathResolver _resolver = new(_ => null);

    [Theory]
    [InlineData("saves/game/main", true)]
    [InlineData("saves/game/settings.dat", true)]
    [InlineData("../escape", false)]
    [InlineData("saves/../secret", false)]
    [InlineData("/absolute", false)]
    [InlineData(@"C:\Windows\System32", false)]
    [InlineData("", false)]
    public void IsSafeRemotePath_ValidatesTraversalAndRoots(string remotePath, bool expected)
    {
        _resolver.IsSafeRemotePath(remotePath).Should().Be(expected);
    }

    [Fact]
    public void MapRemotePathToRepository_MapsInsideRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncPathRoot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mapped = _resolver.MapRemotePathToRepository(root, "saves/game/main");
            mapped.Should().Be(Path.GetFullPath(Path.Combine(root, "saves", "game", "main")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MapRemotePathToRepository_RejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncPathRoot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var act = () => _resolver.MapRemotePathToRepository(root, "../outside");
            act.Should().Throw<GameSync.Core.Errors.PathTraversalException>();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetRepositoryRelativePath_ReturnsForwardSlashes()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncPathRoot", Guid.NewGuid().ToString("N"));
        var absolute = Path.Combine(root, "saves", "game", "file.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllText(absolute, "x");
        try
        {
            _resolver.GetRepositoryRelativePath(root, absolute).Should().Be("saves/game/file.dat");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Normalize_UnifiesSeparators()
    {
        var path = _resolver.Normalize("C:/Temp/GameSync/../GameSync/data");
        path.Should().Be(Path.GetFullPath(@"C:\Temp\GameSync\data"));
    }

    [Fact]
    public void Resolve_ExpandsKnownVariables()
    {
        var resolver = new PathResolver(name => name == "USERPROFILE" ? @"D:\Users\Demo" : null);
        resolver.Resolve("%USERPROFILE%/Saved Games/X")
            .Should().Be(Path.GetFullPath(@"D:\Users\Demo\Saved Games\X"));
    }

    [Fact]
    public void IsAllowedLocalSaveTarget_RejectsSshAndGameSyncStorage()
    {
        var resolver = new PathResolver(name => name switch
        {
            "USERPROFILE" => @"D:\Users\Demo",
            "WINDIR" => @"C:\Windows",
            _ => null
        });

        resolver.IsAllowedLocalSaveTarget(@"D:\Users\Demo\.ssh\id_rsa").Should().BeFalse();
        resolver.IsAllowedLocalSaveTarget(@"D:\Users\Demo\AppData\Local\GameSync\repositories\me__saves\file")
            .Should().BeFalse();
        resolver.IsAllowedLocalSaveTarget(@"C:\Windows\System32\config").Should().BeFalse();
        resolver.IsAllowedLocalSaveTarget(@"D:\Users\Demo\Saved Games\Demo").Should().BeTrue();
    }
}
