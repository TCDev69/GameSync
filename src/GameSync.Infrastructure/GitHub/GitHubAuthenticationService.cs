using GameSync.Core.Abstractions;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Errors;
using GameSync.Core.GitHub;
using GameSync.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.GitHub;

public sealed class GitHubAuthenticationService : IGitHubAuthenticationService
{
    private readonly IGitHubOAuthClient _oauthClient;
    private readonly IGitHubApiClient _apiClient;
    private readonly ICredentialStore _credentialStore;
    private readonly IUriLauncher _uriLauncher;
    private readonly ILogger<GitHubAuthenticationService> _logger;

    public GitHubAuthenticationService(
        IGitHubOAuthClient oauthClient,
        IGitHubApiClient apiClient,
        ICredentialStore credentialStore,
        IUriLauncher uriLauncher,
        ILogger<GitHubAuthenticationService> logger)
    {
        _oauthClient = oauthClient;
        _apiClient = apiClient;
        _credentialStore = credentialStore;
        _uriLauncher = uriLauncher;
        _logger = logger;
    }

    public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default) =>
        _credentialStore.ExistsAsync(GitHubCredentialKeys.AccessToken, cancellationToken);

    public async Task<GitHubDeviceAuthorization> StartAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Authentication start");
        try
        {
            return await _oauthClient.RequestDeviceCodeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (GitHubUnavailableException)
        {
            _logger.LogError("Authentication failure: GitHub unavailable");
            throw;
        }
        catch (Exception ex) when (ex is not GitHubAuthenticationFailedException and not OperationCanceledException)
        {
            _logger.LogError(ex, "Authentication failure");
            throw new GitHubAuthenticationFailedException("Failed to start GitHub authentication.", ex);
        }
    }

    public async Task OpenAuthenticationUrlAsync(GitHubDeviceAuthorization authorization, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        var url = string.IsNullOrWhiteSpace(authorization.VerificationUriComplete)
            ? authorization.VerificationUri
            : authorization.VerificationUriComplete;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            throw new GitHubAuthenticationFailedException("GitHub returned an invalid verification URL.");
        }

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new GitHubAuthenticationFailedException("Refusing to open a non-GitHub verification URL.");
        }

        _logger.LogInformation("Opening GitHub authentication URL");
        await _uriLauncher.OpenAsync(uri, cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteAuthenticationAsync(GitHubDeviceAuthorization authorization, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, authorization.ExpiresInSeconds));
        var interval = TimeSpan.FromSeconds(Math.Max(1, authorization.IntervalSeconds));

        _logger.LogInformation("Polling GitHub authentication status");
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? token;
            try
            {
                token = await _oauthClient.TryGetAccessTokenAsync(authorization.DeviceCode, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (GitHubAuthenticationFailedException)
            {
                _logger.LogError("Authentication failure during token poll");
                throw;
            }

            if (!string.IsNullOrWhiteSpace(token))
            {
                await _credentialStore.StoreSecretAsync(GitHubCredentialKeys.AccessToken, token, cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogInformation("Authentication success");
                return;
            }

            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        _logger.LogError("Authentication failure: timed out");
        throw new GitHubAuthenticationFailedException("GitHub authentication timed out before completion.");
    }

    public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var authorization = await StartAuthenticationAsync(cancellationToken).ConfigureAwait(false);
        await OpenAuthenticationUrlAsync(authorization, cancellationToken).ConfigureAwait(false);
        await CompleteAuthenticationAsync(authorization, cancellationToken).ConfigureAwait(false);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _credentialStore.DeleteSecretAsync(GitHubCredentialKeys.AccessToken, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Signed out of GitHub");
    }

    public async Task<GitHubUser> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _apiClient.GetAuthenticatedUserAsync(token, cancellationToken).ConfigureAwait(false);
        }
        catch (GitHubAuthenticationFailedException)
        {
            _logger.LogError("Authentication failure: token rejected while resolving user");
            throw;
        }
    }

    private async Task<string> RequireTokenAsync(CancellationToken cancellationToken)
    {
        var token = await _credentialStore.RetrieveSecretAsync(GitHubCredentialKeys.AccessToken, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new GitHubAuthenticationFailedException("Not authenticated with GitHub.");
        }

        return token;
    }
}
