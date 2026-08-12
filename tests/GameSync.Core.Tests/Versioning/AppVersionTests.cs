using GameSync.Core.Versioning;

namespace GameSync.Core.Tests.Versioning;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("v1.2.3", true, "1.2.3.0")]
    [InlineData("1.0.0", true, "1.0.0.0")]
    [InlineData("2.1", true, "2.1.0.0")]
    [InlineData("1.2.3-beta.1", true, "1.2.3.0")]
    [InlineData("not-a-version", false, "0.0.0.0")]
    public void TryParseTag_ParsesSemanticTags(string input, bool expectedOk, string expected)
    {
        var ok = AppVersion.TryParseTag(input, out var version);
        Assert.Equal(expectedOk, ok);
        if (expectedOk)
        {
            Assert.Equal(Version.Parse(expected), version);
        }
    }

    [Fact]
    public void IsNewer_ComparesCorrectly()
    {
        Assert.True(AppVersion.IsNewer(Version.Parse("1.0.1.0"), Version.Parse("1.0.0.0")));
        Assert.False(AppVersion.IsNewer(Version.Parse("1.0.0.0"), Version.Parse("1.0.0.0")));
        Assert.False(AppVersion.IsNewer(Version.Parse("0.9.9.0"), Version.Parse("1.0.0.0")));
    }

    [Fact]
    public void Semantic_IsNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppVersion.Semantic));
    }
}
