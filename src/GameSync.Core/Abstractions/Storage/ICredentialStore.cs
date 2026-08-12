namespace GameSync.Core.Abstractions.Storage;

/// <summary>
/// Secure secret storage. Implementations must use Windows Credential Manager (or equivalent),
/// never JSON configuration files.
/// </summary>
public interface ICredentialStore
{
    Task StoreSecretAsync(string key, string secret, CancellationToken cancellationToken = default);

    Task<string?> RetrieveSecretAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
