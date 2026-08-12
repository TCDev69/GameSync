using FluentAssertions;
using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Errors;
using GameSync.Core.GitHub;
using GameSync.Core.Models;
using GameSync.Infrastructure.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GameSync.Infrastructure.Tests.GitHub;

public sealed class GitHubAuthenticationServiceTests
{
    [Fact]
    public async Task Authenticate_Success_StoresTokenWithoutExposingToCaller()
    {
        var oauth = Substitute.For<IGitHubOAuthClient>();
        var api = Substitute.For<IGitHubApiClient>();
        var credentials = Substitute.For<ICredentialStore>();
        var launcher = Substitute.For<IUriLauncher>();

        var device = new GitHubDeviceAuthorization
        {
            UserCode = "ABCD-1234",
            DeviceCode = "secret-device-code",
            VerificationUri = "https://github.com/login/device",
            VerificationUriComplete = "https://github.com/login/device?user_code=ABCD-1234",
            ExpiresInSeconds = 900,
            IntervalSeconds = 1
        };

        oauth.RequestDeviceCodeAsync(Arg.Any<CancellationToken>()).Returns(device);
        oauth.TryGetAccessTokenAsync("secret-device-code", Arg.Any<CancellationToken>())
            .Returns("access-token-value");
        api.GetAuthenticatedUserAsync("access-token-value", Arg.Any<CancellationToken>())
            .Returns(new GitHubUser { Login = "octocat", Id = 1 });

        var service = new GitHubAuthenticationService(oauth, api, credentials, launcher, NullLogger<GitHubAuthenticationService>.Instance);
        await service.AuthenticateAsync();

        await credentials.Received(1).StoreSecretAsync(GitHubCredentialKeys.AccessToken, "access-token-value", Arg.Any<CancellationToken>());
        await launcher.Received(1).OpenAsync(
            Arg.Is<Uri>(u => u.Host == "github.com"),
            Arg.Any<CancellationToken>());

        credentials.ExistsAsync(GitHubCredentialKeys.AccessToken, Arg.Any<CancellationToken>()).Returns(true);
        credentials.RetrieveSecretAsync(GitHubCredentialKeys.AccessToken, Arg.Any<CancellationToken>()).Returns("access-token-value");
        (await service.IsAuthenticatedAsync()).Should().BeTrue();
        (await service.GetAuthenticatedUserAsync()).Login.Should().Be("octocat");
    }

    [Fact]
    public async Task Authenticate_Failure_WhenAccessDenied()
    {
        var oauth = Substitute.For<IGitHubOAuthClient>();
        oauth.RequestDeviceCodeAsync(Arg.Any<CancellationToken>()).Returns(new GitHubDeviceAuthorization
        {
            UserCode = "ABCD-1234",
            DeviceCode = "device",
            VerificationUri = "https://github.com/login/device",
            ExpiresInSeconds = 60,
            IntervalSeconds = 1
        });
        oauth.TryGetAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string?>(_ => throw new GitHubAuthenticationFailedException("GitHub authorization was denied."));

        var service = new GitHubAuthenticationService(
            oauth,
            Substitute.For<IGitHubApiClient>(),
            Substitute.For<ICredentialStore>(),
            Substitute.For<IUriLauncher>(),
            NullLogger<GitHubAuthenticationService>.Instance);

        var act = async () => await service.AuthenticateAsync();
        await act.Should().ThrowAsync<GitHubAuthenticationFailedException>();
    }

    [Fact]
    public async Task SignOut_DeletesCredential()
    {
        var credentials = Substitute.For<ICredentialStore>();
        var service = new GitHubAuthenticationService(
            Substitute.For<IGitHubOAuthClient>(),
            Substitute.For<IGitHubApiClient>(),
            credentials,
            Substitute.For<IUriLauncher>(),
            NullLogger<GitHubAuthenticationService>.Instance);

        await service.SignOutAsync();
        await credentials.Received(1).DeleteSecretAsync(GitHubCredentialKeys.AccessToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenAuthenticationUrl_RejectsNonGitHubHost()
    {
        var service = new GitHubAuthenticationService(
            Substitute.For<IGitHubOAuthClient>(),
            Substitute.For<IGitHubApiClient>(),
            Substitute.For<ICredentialStore>(),
            Substitute.For<IUriLauncher>(),
            NullLogger<GitHubAuthenticationService>.Instance);

        var act = async () => await service.OpenAuthenticationUrlAsync(new GitHubDeviceAuthorization
        {
            UserCode = "X",
            DeviceCode = "Y",
            VerificationUri = "https://evil.example/login",
            ExpiresInSeconds = 60,
            IntervalSeconds = 5
        });

        await act.Should().ThrowAsync<GitHubAuthenticationFailedException>();
    }

    [Fact]
    public async Task CompleteAuthentication_HandlesPendingThenSuccess()
    {
        var oauth = Substitute.For<IGitHubOAuthClient>();
        var credentials = Substitute.For<ICredentialStore>();
        oauth.TryGetAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null, (string?)null, "token");

        var service = new GitHubAuthenticationService(
            oauth,
            Substitute.For<IGitHubApiClient>(),
            credentials,
            Substitute.For<IUriLauncher>(),
            NullLogger<GitHubAuthenticationService>.Instance);

        await service.CompleteAuthenticationAsync(new GitHubDeviceAuthorization
        {
            UserCode = "U",
            DeviceCode = "D",
            VerificationUri = "https://github.com/login/device",
            ExpiresInSeconds = 30,
            IntervalSeconds = 1
        });

        await credentials.Received(1).StoreSecretAsync(GitHubCredentialKeys.AccessToken, "token", Arg.Any<CancellationToken>());
    }
}
