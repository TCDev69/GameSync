using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Errors;
using GameSync.Core.GitHub;
using GameSync.Core.Models;
using GameSync.Core.Options;
using GameSync.Infrastructure.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameSync.Infrastructure.GitHub;

public sealed class HttpGitHubApiClient : IGitHubApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GameSyncOptions _options;
    private readonly ILogger<HttpGitHubApiClient> _logger;

    public HttpGitHubApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<GameSyncOptions> options,
        ILogger<HttpGitHubApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GitHubUser> GetAuthenticatedUserAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        using var request = CreateRequest(HttpMethod.Get, "user", accessToken);
        var payload = await SendAsync(request, GameSyncJsonContext.Default.UserResponse, cancellationToken).ConfigureAwait(false);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Login))
        {
            throw new GitHubAuthenticationFailedException("GitHub did not return an authenticated user.");
        }

        return new GitHubUser
        {
            Login = payload.Login,
            Id = payload.Id,
            Name = payload.Name,
            AvatarUrl = payload.AvatarUrl
        };
    }

    public async Task<IReadOnlyList<RepositoryConfiguration>> GetRepositoriesAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        var results = new List<RepositoryConfiguration>();
        var page = 1;

        while (true)
        {
            using var request = CreateRequest(HttpMethod.Get, $"user/repos?per_page=100&page={page}&affiliation=owner,collaborator,organization_member&sort=updated", accessToken);
            var payload = await SendAsync(request, GameSyncJsonContext.Default.ListRepoResponse, cancellationToken).ConfigureAwait(false);
            if (payload is null || payload.Count == 0)
            {
                break;
            }

            foreach (var repo in payload)
            {
                if (string.IsNullOrWhiteSpace(repo.Name) || string.IsNullOrWhiteSpace(repo.Owner?.Login))
                {
                    continue;
                }

                results.Add(ToConfiguration(repo));
            }

            if (payload.Count < 100)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation("Listed {Count} GitHub repositories for authenticated user", results.Count);
        return results;
    }

    public async Task<RepositoryConfiguration> GetRepositoryAsync(string accessToken, string owner, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        GitHubRepositoryValidator.ValidateOwner(owner);
        GitHubRepositoryValidator.ValidateRepositoryName(name);

        using var request = CreateRequest(HttpMethod.Get, $"repos/{owner}/{name}", accessToken);
        try
        {
            var payload = await SendAsync(request, GameSyncJsonContext.Default.RepoResponse, cancellationToken).ConfigureAwait(false);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Name))
            {
                throw new RepositoryUnavailableException($"Repository '{owner}/{name}' was not found.");
            }

            return ToConfiguration(payload);
        }
        catch (GitHubAuthenticationFailedException)
        {
            throw;
        }
    }

    private async Task<T?> SendAsync<T>(HttpRequestMessage request, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("GitHubApi");
        client.BaseAddress ??= new Uri(_options.GitHubApiBaseUrl);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new GitHubUnavailableException("GitHub API is unreachable.", ex);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new GitHubAuthenticationFailedException("GitHub authentication expired or is invalid. Sign in again.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogError("GitHub API returned forbidden for {Path}", request.RequestUri?.AbsolutePath);
            throw new RepositoryUnavailableException("GitHub denied access to the requested resource.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new RepositoryUnavailableException("GitHub repository was not found or is inaccessible.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GitHub API error {StatusCode} for {Path}", (int)response.StatusCode, request.RequestUri?.AbsolutePath);
            throw new GitHubUnavailableException($"GitHub API returned status {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string accessToken)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        // Authorization header is set on the request only; never logged.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("GameSync");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private RepositoryConfiguration ToConfiguration(RepoResponse repo)
    {
        var owner = repo.Owner?.Login ?? throw new RepositoryUnavailableException("Repository owner is missing.");
        var config = new RepositoryConfiguration
        {
            Owner = owner,
            Name = repo.Name!,
            CloneUrl = repo.CloneUrl,
            DefaultBranch = string.IsNullOrWhiteSpace(repo.DefaultBranch) ? _options.DefaultBranch : repo.DefaultBranch,
            IsPrivate = repo.Private
        };
        GitHubRepositoryValidator.Validate(config);
        return config;
    }

    internal sealed class UserResponse
    {
        [JsonPropertyName("login")]
        public string? Login { get; set; }

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }
    }

    internal sealed class RepoResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("clone_url")]
        public string? CloneUrl { get; set; }

        [JsonPropertyName("default_branch")]
        public string? DefaultBranch { get; set; }

        [JsonPropertyName("private")]
        public bool Private { get; set; }

        [JsonPropertyName("owner")]
        public OwnerResponse? Owner { get; set; }
    }

    internal sealed class OwnerResponse
    {
        [JsonPropertyName("login")]
        public string? Login { get; set; }
    }
}
