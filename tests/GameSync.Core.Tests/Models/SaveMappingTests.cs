using FluentAssertions;
using GameSync.Core.Services;

namespace GameSync.Core.Tests.Models;

public sealed class SaveMappingTests
{
    [Theory]
    [InlineData("Cyberpunk 2077", "cyberpunk_2077")]
    [InlineData("Minecraft", "minecraft")]
    [InlineData("  The Witcher 3  ", "the_witcher_3")]
    public void SuggestGameId_NormalizesTitle(string title, string expected)
    {
        SaveMapping.SuggestGameId(title).Should().Be(expected);
    }

    [Fact]
    public void BuildDefaultRemotePath_UsesForwardSlashes()
    {
        SaveMapping.BuildDefaultRemotePath("cyberpunk_2077", "main")
            .Should().Be("saves/cyberpunk_2077/main");
    }
}
