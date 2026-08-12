using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using GameSync.Core.Abstractions.Storage;
using Microsoft.Extensions.Logging;

namespace GameSync.Infrastructure.Credentials;

/// <summary>
/// Stores secrets in Windows Credential Manager via CredWrite/CredRead/CredDelete.
/// Tokens are never written to JSON files.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    private const string TargetPrefix = "GameSync/";
    private readonly ILogger<WindowsCredentialStore> _logger;

    public WindowsCredentialStore(ILogger<WindowsCredentialStore> logger)
    {
        _logger = logger;
    }

    public Task StoreSecretAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        cancellationToken.ThrowIfCancellationRequested();

        var target = NormalizeTarget(key);
        var secretBytes = Encoding.Unicode.GetBytes(secret);

        var credential = new NativeCredential
        {
            Type = CredentialType.Generic,
            TargetName = target,
            CredentialBlobSize = (uint)secretBytes.Length,
            CredentialBlob = Marshal.AllocHGlobal(secretBytes.Length),
            Persist = CredentialPersistence.LocalMachine,
            UserName = Environment.UserName,
            AttributeCount = 0,
            Attributes = IntPtr.Zero,
            Comment = "GameSync secret",
            TargetAlias = null
        };

        try
        {
            Marshal.Copy(secretBytes, 0, credential.CredentialBlob, secretBytes.Length);
            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to store credential '{key}'.");
            }

            _logger.LogInformation("Stored credential for key {CredentialKey}", key);
        }
        finally
        {
            if (credential.CredentialBlob != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(credential.CredentialBlob);
            }
        }

        return Task.CompletedTask;
    }

    public Task<string?> RetrieveSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        var target = NormalizeTarget(key);
        if (!CredRead(target, CredentialType.Generic, 0, out var credentialPtr) || credentialPtr == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1168) // ERROR_NOT_FOUND
            {
                return Task.FromResult<string?>(null);
            }

            throw new Win32Exception(error, $"Failed to read credential '{key}'.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPtr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return Task.FromResult<string?>(null);
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            var secret = Encoding.Unicode.GetString(bytes);
            return Task.FromResult<string?>(secret);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        var target = NormalizeTarget(key);
        if (!CredDelete(target, CredentialType.Generic, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1168)
            {
                throw new Win32Exception(error, $"Failed to delete credential '{key}'.");
            }
        }

        _logger.LogInformation("Deleted credential for key {CredentialKey}", key);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var secret = await RetrieveSecretAsync(key, cancellationToken).ConfigureAwait(false);
        return secret is not null;
    }

    private static string NormalizeTarget(string key) =>
        key.StartsWith(TargetPrefix, StringComparison.OrdinalIgnoreCase) ? key : TargetPrefix + key;

    private enum CredentialType : uint
    {
        Generic = 1
    }

    private enum CredentialPersistence : uint
    {
        LocalMachine = 2
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public CredentialType Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public CredentialPersistence Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref NativeCredential userCredential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, CredentialType type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);
}
