using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.Errors;
using GameSync.Core.GitHub;
using GameSync.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.GitHub;

public sealed class GitHubService : IGitHubService
{
    private readonly IGitHubApiClient _apiClient;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(
        IGitHubApiClient apiClient,
        ICredentialStore credentialStore,
        ILogger<GitHubService> logger)
    {
        _apiClient = apiClient;
        _credentialStore = credentialStore;
        _logger = logger;
    }

    public async Task<GitHubUser> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken).ConfigureAwait(false);
        return await _apiClient.GetAuthenticatedUserAsync(token, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RepositoryConfiguration>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var token = await RequireTokenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _apiClient.GetRepositoriesAsync(token, cancellationToken).ConfigureAwait(false);
        }
        catch (GitHubUnavailableException)
        {
            _logger.LogError("GitHub API unavailable while listing repositories");
            throw;
        }
    }

    public async Task<RepositoryConfiguration> GetRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        GitHubRepositoryValidator.ValidateOwner(owner);
        GitHubRepositoryValidator.ValidateRepositoryName(name);
        var token = await RequireTokenAsync(cancellationToken).ConfigureAwait(false);
        return await _apiClient.GetRepositoryAsync(token, owner, name, cancellationToken).ConfigureAwait(false);
    }

    public async Task VerifyRepositoryAccessAsync(string owner, string name, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying access to repository {Owner}/{Name}", owner, name);
        var repo = await GetRepositoryAsync(owner, name, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(repo.Owner, owner, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(repo.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            throw new RepositoryUnavailableException($"Repository identity mismatch for '{owner}/{name}'.");
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
