using FluentAssertions;
using GameSync.Core.Configuration;
using GameSync.Core.Models;

namespace GameSync.Core.Tests.Configuration;

public sealed class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator _validator = new();

    [Fact]
    public void Validate_ValidGamesConfiguration_ReturnsNoErrors()
    {
        var config = new GamesConfiguration
        {
            Games =
            [
                new Game
                {
                    Id = "cyberpunk_2077",
                    Title = "Cyberpunk 2077",
                    SaveLocations =
                    [
                        new SaveLocation
                        {
                            Id = "main",
                            Type = SaveLocationType.Directory,
                            RemotePath = "saves/cyberpunk_2077/main",
                            LocalPath = "%USERPROFILE%/Saved Games/CD Projekt Red/Cyberpunk 2077"
                        }
                    ]
                }
            ]
        };

        _validator.Validate(config).Should().BeEmpty();
    }

    [Fact]
    public void Validate_DuplicateGameIds_ReturnsError()
    {
        var config = new GamesConfiguration
        {
            Games =
            [
                new Game { Id = "game_a", Title = "A", SaveLocations = [ValidSave("main")] },
                new Game { Id = "game_a", Title = "B", SaveLocations = [ValidSave("main")] }
            ]
        };

        _validator.Validate(config).Should().Contain(e => e.Contains("Duplicate game id", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_InvalidGameId_ReturnsError()
    {
        var config = new GamesConfiguration
        {
            Games =
            [
                new Game { Id = "Bad Id!", Title = "Bad", SaveLocations = [ValidSave("main")] }
            ]
        };

        _validator.Validate(config).Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_MachineConfiguration_RequiresExecutable()
    {
        var config = new MachineConfiguration
        {
            MachineId = "DESKTOP",
            Games =
            {
                ["cyberpunk_2077"] = new GameLaunchConfiguration { Executable = "" }
            }
        };

        _validator.Validate(config).Should().Contain(e => e.Contains("executable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ValidMachineConfiguration_ReturnsNoErrors()
    {
        var config = new MachineConfiguration
        {
            MachineId = "DESKTOP",
            Games =
            {
                ["cyberpunk_2077"] = new GameLaunchConfiguration
                {
                    Executable = @"D:\Games\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe"
                }
            }
        };

        _validator.Validate(config).Should().BeEmpty();
    }

    private static SaveLocation ValidSave(string id) => new()
    {
        Id = id,
        RemotePath = $"saves/game/{id}",
        LocalPath = "%USERPROFILE%/Saves"
    };
}
