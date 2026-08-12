using FluentAssertions;
using GameSync.Core.Services;

namespace GameSync.Core.Tests.Services;

public sealed class LaunchTargetTests
{
    [Theory]
    [InlineData("steam://run/1091500", true)]
    [InlineData("steam://rungameid/14214937849538543616", true)]
    [InlineData(@"C:\Games\game.exe", false)]
    [InlineData("https://example.com", false)]
    public void IsProtocolUri_ClassifiesTargets(string value, bool expected) =>
        LaunchTarget.IsProtocolUri(value).Should().Be(expected);

    [Fact]
    public void TryNormalizeSteamInput_AcceptsNumericAppId()
    {
        LaunchTarget.TryNormalizeSteamInput("1091500", out var uri).Should().BeTrue();
        uri.Should().Be("steam://run/1091500");
    }

    [Fact]
    public void BuildSteamRunGameIdUri_UsesRunGameIdScheme() =>
        LaunchTarget.BuildSteamRunGameIdUri("14214937849538543616")
            .Should().Be("steam://rungameid/14214937849538543616");
}
