using FluentAssertions;
using GameSync.Core.Commands;

namespace GameSync.Core.Tests.Commands;

public sealed class AppCommandParserTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsDashboard()
    {
        var command = AppCommandParser.Parse(Array.Empty<string>());
        command.Kind.Should().Be(AppCommandKind.Dashboard);
        command.GameId.Should().BeNull();
    }

    [Fact]
    public void Parse_GameSwitch_ReturnsLaunchGame()
    {
        var command = AppCommandParser.Parse(["--game", "cyberpunk_2077"]);
        command.Kind.Should().Be(AppCommandKind.LaunchGame);
        command.GameId.Should().Be("cyberpunk_2077");
    }

    [Fact]
    public void Parse_SyncWithoutGame_ReturnsSyncAll()
    {
        var command = AppCommandParser.Parse(["--sync"]);
        command.Kind.Should().Be(AppCommandKind.SyncAll);
        command.GameId.Should().BeNull();
    }

    [Fact]
    public void Parse_SyncWithGame_ReturnsSyncGame()
    {
        var command = AppCommandParser.Parse(["--sync", "minecraft"]);
        command.Kind.Should().Be(AppCommandKind.SyncGame);
        command.GameId.Should().Be("minecraft");
    }

    [Theory]
    [InlineData("--status", AppCommandKind.Status)]
    [InlineData("--settings", AppCommandKind.Settings)]
    [InlineData("--check-update", AppCommandKind.CheckUpdate)]
    [InlineData("--check-updates", AppCommandKind.CheckUpdate)]
    [InlineData("--update", AppCommandKind.InstallUpdate)]
    [InlineData("--help", AppCommandKind.Help)]
    [InlineData("-?", AppCommandKind.Help)]
    public void Parse_SimpleSwitches(string arg, AppCommandKind expected)
    {
        var command = AppCommandParser.Parse([arg]);
        command.Kind.Should().Be(expected);
    }

    [Fact]
    public void GetHelpText_IncludesCommands()
    {
        var help = AppCommandParser.GetHelpText();
        help.Should().Contain("--game");
        help.Should().Contain("--sync");
        help.Should().Contain("--help");
    }

    [Fact]
    public void Parse_GameWithoutValue_Throws()
    {
        var act = () => AppCommandParser.Parse(["--game"]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_UnknownArgs_Throws()
    {
        var act = () => AppCommandParser.Parse(["--unknown"]);
        act.Should().Throw<ArgumentException>();
    }
}
