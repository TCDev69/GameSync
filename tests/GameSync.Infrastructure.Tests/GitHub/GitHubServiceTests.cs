using FluentAssertions;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Errors;
using GameSync.Core.GitHub;
using GameSync.Core.Models;
using GameSync.Infrastructure.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GameSync.Infrastructure.Tests.GitHub;

public sealed class GitHubServiceTests
{
    [Fact]
    public async Task GetRepositories_ReturnsMappedRepositories()
    {
        var api = Substitute.For<IGitHubApiClient>();
        var credentials = Substitute.For<ICredentialStore>();
        credentials.RetrieveSecretAsync(GitHubCredentialKeys.AccessToken, Arg.Any<CancellationToken>())
            .Returns("token");
        api.GetRepositoriesAsync("token", Arg.Any<CancellationToken>()).Returns(
        [
            new RepositoryConfiguration { Owner = "me", Name = "gamesync-saves", DefaultBranch = "main", IsPrivate = true }
        ]);

        var service = new GitHubService(api, credentials, NullLogger<GitHubService>.Instance);
        var repos = await service.GetRepositoriesAsync();

        repos.Should().ContainSingle(r => r.Owner == "me" && r.Name == "gamesync-saves");
    }

    [Fact]
    public async Task GetRepository_InvalidName_ThrowsBeforeNetwork()
    {
        var service = new GitHubService(
            Substitute.For<IGitHubApiClient>(),
            Substitute.For<ICredentialStore>(),
            NullLogger<GitHubService>.Instance);

        var act = async () => await service.GetRepositoryAsync("me", "../evil");
        await act.Should().ThrowAsync<RepositoryUnavailableException>();
    }

    [Fact]
    public async Task VerifyRepositoryAccess_MissingAccess_Throws()
    {
        var api = Substitute.For<IGitHubApiClient>();
        var credentials = Substitute.For<ICredentialStore>();
        credentials.RetrieveSecretAsync(GitHubCredentialKeys.AccessToken, Arg.Any<CancellationToken>())
            .Returns("token");
        api.GetRepositoryAsync("token", "me", "private-saves", Arg.Any<CancellationToken>())
            .Returns<RepositoryConfiguration>(_ => throw new RepositoryUnavailableException("denied"));

        var service = new GitHubService(api, credentials, NullLogger<GitHubService>.Instance);
        var act = async () => await service.VerifyRepositoryAccessAsync("me", "private-saves");
        await act.Should().ThrowAsync<RepositoryUnavailableException>();
    }

    [Fact]
    public async Task GetAuthenticatedUser_WithoutToken_Throws()
    {
        var credentials = Substitute.For<ICredentialStore>();
        credentials.RetrieveSecretAsync(GitHubCredentialKeys.AccessToken, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var service = new GitHubService(Substitute.For<IGitHubApiClient>(), credentials, NullLogger<GitHubService>.Instance);
        var act = async () => await service.GetAuthenticatedUserAsync();
        await act.Should().ThrowAsync<GitHubAuthenticationFailedException>();
    }

    [Fact]
    public async Task GetRepositories_Offline_PropagatesUnavailable()
    {
        var api = Substitute.For<IGitHubApiClient>();
        var credentials = Substitute.For<ICredentialStore>();
        credentials.RetrieveSecretAsync(GitHubCredentialKeys.AccessToken, Arg.Any<CancellationToken>())
            .Returns("token");
        api.GetRepositoriesAsync("token", Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<RepositoryConfiguration>>(_ => throw new GitHubUnavailableException("offline"));

        var service = new GitHubService(api, credentials, NullLogger<GitHubService>.Instance);
        var act = async () => await service.GetRepositoriesAsync();
        await act.Should().ThrowAsync<GitHubUnavailableException>();
    }
}
