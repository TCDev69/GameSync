using FluentAssertions;
using GameSync.Core.Abstractions.Storage;
using GameSync.Core.GitHub;
using GameSync.Infrastructure.Credentials;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameSync.Infrastructure.Tests.GitHub;

public sealed class CredentialStoreAbstractionTests
{
    [Fact]
    public async Task InMemoryCredentialStore_RoundTripsWithoutJsonFiles()
    {
        ICredentialStore store = new InMemoryCredentialStore();
        await store.StoreSecretAsync(GitHubCredentialKeys.AccessToken, "secret-value");
        (await store.ExistsAsync(GitHubCredentialKeys.AccessToken)).Should().BeTrue();
        (await store.RetrieveSecretAsync(GitHubCredentialKeys.AccessToken)).Should().Be("secret-value");
        await store.DeleteSecretAsync(GitHubCredentialKeys.AccessToken);
        (await store.ExistsAsync(GitHubCredentialKeys.AccessToken)).Should().BeFalse();
    }

    [Fact]
    public void WindowsCredentialStore_CanBeConstructed()
    {
        var store = new WindowsCredentialStore(NullLogger<WindowsCredentialStore>.Instance);
        store.Should().NotBeNull();
    }

    private sealed class InMemoryCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public Task StoreSecretAsync(string key, string secret, CancellationToken cancellationToken = default)
        {
            _secrets[key] = secret;
            return Task.CompletedTask;
        }

        public Task<string?> RetrieveSecretAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.TryGetValue(key, out var value) ? value : null);

        public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        {
            _secrets.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.ContainsKey(key));
    }
}
