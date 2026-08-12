using FluentAssertions;
using GameSync.Core.GitHub;
using GameSync.Core.Models;

namespace GameSync.Core.Tests.GitHub;

public sealed class GitHubRepositoryValidatorTests
{
    [Theory]
    [InlineData("octocat", true)]
    [InlineData("a", true)]
    [InlineData("-bad", false)]
    [InlineData("bad..name", false)]
    [InlineData("", false)]
    public void ValidateOwner(string owner, bool valid)
    {
        var act = () => GitHubRepositoryValidator.ValidateOwner(owner);
        if (valid)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<GameSync.Core.Errors.RepositoryUnavailableException>();
        }
    }

    [Theory]
    [InlineData("gamesync-saves", true)]
    [InlineData("repo.git", true)]
    [InlineData("../escape", false)]
    [InlineData("has spaces", false)]
    public void ValidateRepositoryName(string name, bool valid)
    {
        var act = () => GitHubRepositoryValidator.ValidateRepositoryName(name);
        if (valid)
        {
            act.Should().NotThrow();
        }
        else
        {
            act.Should().Throw<GameSync.Core.Errors.RepositoryUnavailableException>();
        }
    }

    [Fact]
    public void ValidateCloneUrl_RequiresHttpsGitHubMatch()
    {
        var act = () => GitHubRepositoryValidator.ValidateCloneUrl(
            "https://evil.example/octocat/gamesync-saves.git",
            "octocat",
            "gamesync-saves");
        act.Should().Throw<GameSync.Core.Errors.RepositoryUnavailableException>();

        GitHubRepositoryValidator.ValidateCloneUrl(
            "https://github.com/octocat/gamesync-saves.git",
            "octocat",
            "gamesync-saves");
    }

    [Fact]
    public void Validate_AcceptsValidConfiguration()
    {
        var config = new RepositoryConfiguration
        {
            Owner = "octocat",
            Name = "gamesync-saves",
            DefaultBranch = "main",
            CloneUrl = "https://github.com/octocat/gamesync-saves.git"
        };

        var act = () => GitHubRepositoryValidator.Validate(config);
        act.Should().NotThrow();
    }
}
