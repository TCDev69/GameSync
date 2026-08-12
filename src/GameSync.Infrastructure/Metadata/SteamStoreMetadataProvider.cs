using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GameSync.Core.Abstractions.Metadata;
using GameSync.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Metadata;

/// <summary>
/// Uses the public Steam Store search API (no API key) for titles and cover art.
/// </summary>
public sealed class SteamStoreMetadataProvider : IGameMetadataProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SteamStoreMetadataProvider> _logger;

    public SteamStoreMetadataProvider(IHttpClientFactory httpClientFactory, ILogger<SteamStoreMetadataProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderId => "steam";

    public async Task<IReadOnlyList<GameSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<GameSearchResult>();
        }

        var client = _httpClientFactory.CreateClient("SteamStore");
        var url = $"api/storesearch/?term={Uri.EscapeDataString(query.Trim())}&l=english&cc=US";
        try
        {
            var response = await client.GetFromJsonAsync<SteamSearchResponse>(url, cancellationToken).ConfigureAwait(false);
            if (response?.Items is null)
            {
                return Array.Empty<GameSearchResult>();
            }

            return response.Items
                .Where(i => i.Id > 0 && !string.IsNullOrWhiteSpace(i.Name))
                .Take(20)
                .Select(i => new GameSearchResult
                {
                    ExternalId = i.Id.ToString(),
                    ProviderId = ProviderId,
                    Title = i.Name!,
                    CoverUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{i.Id}/library_600x900.jpg",
                    Platform = "PC"
                })
                .ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Steam metadata search failed");
            return Array.Empty<GameSearchResult>();
        }
    }

    public async Task<GameMetadata?> GetMetadataAsync(string externalId, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(externalId, out var appId))
        {
            return null;
        }

        var results = await SearchAsync(externalId, cancellationToken).ConfigureAwait(false);
        // Prefer direct cover construction when we only have an id.
        var match = results.FirstOrDefault(r => r.ExternalId == externalId);
        if (match is not null)
        {
            return new GameMetadata
            {
                ExternalId = match.ExternalId,
                ProviderId = ProviderId,
                Title = match.Title,
                CoverUrl = match.CoverUrl,
                Platform = match.Platform
            };
        }

        return new GameMetadata
        {
            ExternalId = externalId,
            ProviderId = ProviderId,
            Title = $"Steam App {appId}",
            CoverUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
            Platform = "PC"
        };
    }

    private sealed class SteamSearchResponse
    {
        [JsonPropertyName("items")]
        public List<SteamSearchItem>? Items { get; set; }
    }

    private sealed class SteamSearchItem
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
