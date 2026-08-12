using FluentAssertions;
using GameSync.Infrastructure.Paths;

namespace GameSync.Infrastructure.Tests.Paths;

public sealed class WindowsPathResolverTests
{
    [Fact]
    public void Resolve_ExpandsUserProfile()
    {
        var resolver = new WindowsPathResolver(name =>
            name.Equals("USERPROFILE", StringComparison.OrdinalIgnoreCase) ? @"C:\Users\Test" : null);

        var resolved = resolver.Resolve("%USERPROFILE%/Saved Games/Test");

        resolved.Should().Be(Path.GetFullPath(@"C:\Users\Test\Saved Games\Test"));
    }

    [Fact]
    public void Resolve_ExpandsLocalAppData()
    {
        var resolver = new WindowsPathResolver(name =>
            name.Equals("LOCALAPPDATA", StringComparison.OrdinalIgnoreCase) ? @"C:\Users\Test\AppData\Local" : null);

        var resolved = resolver.Resolve("%LOCALAPPDATA%/GameSync/cache");

        resolved.Should().Be(Path.GetFullPath(@"C:\Users\Test\AppData\Local\GameSync\cache"));
    }

    [Fact]
    public void IsValidTemplate_AcceptsKnownVariables()
    {
        var resolver = new WindowsPathResolver(_ => null);
        resolver.IsValidTemplate("%USERPROFILE%/Saved Games").Should().BeTrue();
        resolver.IsValidTemplate("%PROGRAMFILES(X86)%/Steam").Should().BeTrue();
    }

    [Fact]
    public void IsValidTemplate_RejectsUnknownVariables()
    {
        var resolver = new WindowsPathResolver(_ => null);
        resolver.IsValidTemplate("%NOT_A_REAL_VAR%/path").Should().BeFalse();
    }

    [Fact]
    public void ToPortableTemplate_ReplacesUserProfilePrefix()
    {
        var resolver = new WindowsPathResolver(name =>
            name.Equals("USERPROFILE", StringComparison.OrdinalIgnoreCase) ? @"C:\Users\Test" : null);

        var portable = resolver.ToPortableTemplate(@"C:\Users\Test\Saved Games\Cyberpunk 2077");

        portable.Should().Be("%USERPROFILE%/Saved Games/Cyberpunk 2077");
    }

    [Fact]
    public void ToPortableTemplate_PrefersLongestRoot_LocalAppDataOverUserProfile()
    {
        var resolver = new WindowsPathResolver(name => name.ToUpperInvariant() switch
        {
            "USERPROFILE" => @"C:\Users\Test",
            "LOCALAPPDATA" => @"C:\Users\Test\AppData\Local",
            _ => null
        });

        var portable = resolver.ToPortableTemplate(@"C:\Users\Test\AppData\Local\GameCompany\Saves");

        portable.Should().Be("%LOCALAPPDATA%/GameCompany/Saves");
    }

    [Fact]
    public void ToPortableTemplate_KeepsExistingTokens()
    {
        var resolver = new WindowsPathResolver(_ => null);
        resolver.ToPortableTemplate(@"%USERPROFILE%\Saved Games\X")
            .Should().Be("%USERPROFILE%/Saved Games/X");
    }
}
