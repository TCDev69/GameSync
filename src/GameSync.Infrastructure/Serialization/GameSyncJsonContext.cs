using System.Text.Json.Serialization;
using GameSync.Core.Models;
using GameSync.Infrastructure.GitHub;
using GameSync.Infrastructure.Metadata;
using GameSync.Infrastructure.Updates;

namespace GameSync.Infrastructure.Serialization;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(MachineConfiguration))]
[JsonSerializable(typeof(GamesConfiguration))]
[JsonSerializable(typeof(UiSettings))]
[JsonSerializable(typeof(Dictionary<string, GameLaunchConfiguration>))]
[JsonSerializable(typeof(List<Game>))]
[JsonSerializable(typeof(List<SaveLocation>))]
[JsonSerializable(typeof(HttpGitHubOAuthClient.DeviceCodeResponse))]
[JsonSerializable(typeof(HttpGitHubOAuthClient.AccessTokenResponse))]
[JsonSerializable(typeof(HttpGitHubApiClient.UserResponse))]
[JsonSerializable(typeof(HttpGitHubApiClient.RepoResponse))]
[JsonSerializable(typeof(HttpGitHubApiClient.OwnerResponse))]
[JsonSerializable(typeof(List<HttpGitHubApiClient.RepoResponse>))]
[JsonSerializable(typeof(GitHubReleaseAppUpdateService.GitHubReleaseDto))]
[JsonSerializable(typeof(GitHubReleaseAppUpdateService.GitHubAssetDto))]
[JsonSerializable(typeof(List<GitHubReleaseAppUpdateService.GitHubAssetDto>))]
[JsonSerializable(typeof(SteamStoreMetadataProvider.SteamSearchResponse))]
[JsonSerializable(typeof(SteamStoreMetadataProvider.SteamSearchItem))]
[JsonSerializable(typeof(List<SteamStoreMetadataProvider.SteamSearchItem>))]
internal partial class GameSyncJsonContext : JsonSerializerContext;
