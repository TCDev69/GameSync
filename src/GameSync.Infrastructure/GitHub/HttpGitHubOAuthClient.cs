using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameSync.Core.Abstractions.GitHub;
using GameSync.Core.Errors;
using GameSync.Core.Models;
using GameSync.Core.Options;
using GameSync.Infrastructure.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameSync.Infrastructure.GitHub;

public sealed class HttpGitHubOAuthClient : IGitHubOAuthClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GameSyncOptions _options;
    private readonly ILogger<HttpGitHubOAuthClient> _logger;

    public HttpGitHubOAuthClient(
        IHttpClientFactory httpClientFactory,
        IOptions<GameSyncOptions> options,
        ILogger<HttpGitHubOAuthClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GitHubDeviceAuthorization> RequestDeviceCodeAsync(CancellationToken cancellationToken = default)
    {
        EnsureClientId();
        var client = CreateOAuthClient();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.GitHubClientId,
            ["scope"] = _options.GitHubScopes
        });

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(new Uri("login/device/code", UriKind.Relative), content, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new GitHubUnavailableException("GitHub OAuth endpoint is unreachable.", ex);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync(
                stream,
                GameSyncJsonContext.Default.DeviceCodeResponse,
                cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.DeviceCode))
        {
            _logger.LogError("GitHub device code request failed with status {StatusCode}", (int)response.StatusCode);
            throw new GitHubAuthenticationFailedException("Failed to start GitHub device authorization.");
        }

        _logger.LogInformation("GitHub device authorization started (user code issued)");
        return new GitHubDeviceAuthorization
        {
            DeviceCode = payload.DeviceCode,
            UserCode = payload.UserCode ?? string.Empty,
            VerificationUri = payload.VerificationUri ?? "https://github.com/login/device",
            VerificationUriComplete = payload.VerificationUriComplete,
            ExpiresInSeconds = payload.ExpiresIn <= 0 ? 900 : payload.ExpiresIn,
            IntervalSeconds = payload.Interval <= 0 ? 5 : payload.Interval
        };
    }

    public async Task<string?> TryGetAccessTokenAsync(string deviceCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceCode);
        EnsureClientId();
        var client = CreateOAuthClient();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.GitHubClientId,
            ["device_code"] = deviceCode,
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
        });

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(new Uri("login/oauth/access_token", UriKind.Relative), content, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new GitHubUnavailableException("GitHub OAuth token endpoint is unreachable.", ex);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync(
                stream,
                GameSyncJsonContext.Default.AccessTokenResponse,
                cancellationToken)
            .ConfigureAwait(false);

        if (payload is null)
        {
            throw new GitHubAuthenticationFailedException("Invalid token response from GitHub.");
        }

        if (!string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return payload.AccessToken;
        }

        return payload.Error switch
        {
            "authorization_pending" => null,
            "slow_down" => null,
            "expired_token" => throw new GitHubAuthenticationFailedException("GitHub device code expired. Restart authentication."),
            "access_denied" => throw new GitHubAuthenticationFailedException("GitHub authorization was denied."),
            "unsupported_grant_type" => throw new GitHubAuthenticationFailedException("GitHub rejected the device grant type."),
            "incorrect_device_code" => throw new GitHubAuthenticationFailedException("GitHub rejected the device code."),
            _ when !string.IsNullOrWhiteSpace(payload.Error) =>
                throw new GitHubAuthenticationFailedException($"GitHub authentication failed ({payload.Error})."),
            _ => throw new GitHubAuthenticationFailedException("GitHub authentication failed with an unknown response.")
        };
    }

    private HttpClient CreateOAuthClient()
    {
        var client = _httpClientFactory.CreateClient("GitHubOAuth");
        client.BaseAddress ??= new Uri(_options.GitHubOAuthBaseUrl);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private void EnsureClientId()
    {
        if (string.IsNullOrWhiteSpace(_options.GitHubClientId))
        {
            throw new GitHubAuthenticationFailedException(
                "GitHub client ID is not configured.");
        }
    }

    internal sealed class DeviceCodeResponse
    {
        [JsonPropertyName("device_code")]
        public string? DeviceCode { get; set; }

        [JsonPropertyName("user_code")]
        public string? UserCode { get; set; }

        [JsonPropertyName("verification_uri")]
        public string? VerificationUri { get; set; }

        [JsonPropertyName("verification_uri_complete")]
        public string? VerificationUriComplete { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; }
    }

    internal sealed class AccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }
}
