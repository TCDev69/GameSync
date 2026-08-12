using FluentAssertions;
using GameSync.Core.Options;

namespace GameSync.Core.Tests.Options;

public sealed class GameSyncOptionsTests
{
    [Fact]
    public void NormalizeUpdateFeed_LegacyTcDevOwner_IsCorrectedToTcDev69()
    {
        var options = new GameSyncOptions
        {
            UpdateReleasesOwner = "TCDev",
            UpdateReleasesRepo = GameSyncOptions.DefaultUpdateReleasesRepo
        };

        options.NormalizeUpdateFeed();

        options.UpdateReleasesOwner.Should().Be(GameSyncOptions.DefaultUpdateReleasesOwner);
    }

    [Fact]
    public void NormalizeUpdateFeed_CustomFork_IsLeftUnchanged()
    {
        var options = new GameSyncOptions
        {
            UpdateReleasesOwner = "TCDev",
            UpdateReleasesRepo = "MyFork"
        };

        options.NormalizeUpdateFeed();

        options.UpdateReleasesOwner.Should().Be("TCDev");
    }
}
