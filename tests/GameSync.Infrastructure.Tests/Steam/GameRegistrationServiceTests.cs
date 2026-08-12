using GameSync.Core.Abstractions.Games;
using GameSync.Core.Models;
using GameSync.Infrastructure.Games;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.Configuration;
using GameSync.Core.Abstractions.Git;

namespace GameSync.Infrastructure.Tests.Steam;

public sealed class GameRegistrationServiceTests
{
    [Fact]
    public void FindDuplicateCandidates_MatchesByExternalId()
    {
        var svc = CreateService();
        var existing = new List<Game>
        {
            new()
            {
                Id = "dota_2",
                Title = "Dota 2",
                MetadataExternalId = "570",
                MetadataProviderId = "steam",
                SaveLocations = []
            }
        };

        var result = svc.FindDuplicateCandidates(existing, "Some Other Title", "570");

        Assert.Single(result);
        Assert.Equal("dota_2", result[0].Id);
    }

    [Fact]
    public void FindDuplicateCandidates_MatchesByTitle()
    {
        var svc = CreateService();
        var existing = new List<Game>
        {
            new()
            {
                Id = "dota_2",
                Title = "Dota 2",
                SaveLocations = []
            }
        };

        var result = svc.FindDuplicateCandidates(existing, "Dota 2", null);

        Assert.Single(result);
    }

    [Fact]
    public void FindDuplicateCandidates_NoMatch_ReturnsEmpty()
    {
        var svc = CreateService();
        var existing = new List<Game>
        {
            new()
            {
                Id = "dota_2",
                Title = "Dota 2",
                SaveLocations = []
            }
        };

        var result = svc.FindDuplicateCandidates(existing, "Cyberpunk 2077", "1091500");

        Assert.Empty(result);
    }

    private static GameRegistrationService CreateService()
    {
        return new GameRegistrationService(
            Substitute.For<IMachineConfigurationStore>(),
            Substitute.For<ISharedGamesConfigurationStore>(),
            Substitute.For<IPathResolver>(),
            Substitute.For<IGitService>(),
            NullLogger<GameRegistrationService>.Instance);
    }
}
