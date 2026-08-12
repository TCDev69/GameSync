using FluentAssertions;
using GameSync.Core.Services;

namespace GameSync.Core.Tests.Models;

public sealed class SyncCommitMessageTests
{
    [Fact]
    public void ForGameUpdate_WithoutMachine()
    {
        SyncCommitMessage.ForGameUpdate("Cyberpunk 2077")
            .Should().Be("GameSync: Update Cyberpunk 2077 saves");
    }

    [Fact]
    public void ForGameUpdate_WithMachine()
    {
        SyncCommitMessage.ForGameUpdate("Cyberpunk 2077", "DESKTOP")
            .Should().Be("GameSync: Update Cyberpunk 2077 saves from DESKTOP");
    }
}
