using FluentAssertions;
using GameSync.Core.Models;
using GameSync.Core.Services;
using GameSync.Infrastructure.Shortcuts;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameSync.Infrastructure.Tests.Launch;

public sealed class ShortcutServiceTests
{
    [Theory]
    [InlineData("Cyberpunk 2077", "Cyberpunk 2077")]
    [InlineData("Game: Name?", "Game_ Name_")]
    [InlineData("  spaced   name  ", "spaced name")]
    public void SanitizeFileName_RemovesInvalidCharacters(string input, string expected)
    {
        ShortcutNaming.SanitizeFileName(input).Should().Be(expected);
    }

    [Fact]
    public void BuildLaunchArguments_UsesGameSwitch()
    {
        ShortcutNaming.BuildLaunchArguments("cyberpunk_2077").Should().Be("--game cyberpunk_2077");
    }

    [Fact]
    public void GetShortcutPath_UsesSanitizedName()
    {
        var service = new WindowsShortcutService(NullLogger<WindowsShortcutService>.Instance);
        var desktop = service.GetShortcutPath(new ShortcutConfiguration
        {
            GameId = "cyberpunk_2077",
            DisplayName = "Cyberpunk 2077",
            Kind = ShortcutKind.Desktop
        });

        Path.GetFileName(desktop).Should().Be("Cyberpunk 2077.lnk");
        desktop.Should().StartWith(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));

        var startMenu = service.GetShortcutPath(new ShortcutConfiguration
        {
            GameId = "cyberpunk_2077",
            DisplayName = "Cyberpunk 2077",
            Kind = ShortcutKind.StartMenu
        });
        startMenu.Should().Contain(Path.Combine("Programs", "GameSync"));
        service.BuildLaunchArguments("cyberpunk_2077").Should().Be("--game cyberpunk_2077");
    }
}
