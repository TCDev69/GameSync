namespace GameSync.Core.Models;

/// <summary>
/// Public device-flow challenge shown to the user. Never contains the access token.
/// Device code is kept for polling only and must not be logged or shown in UI dumps.
/// </summary>
public sealed class GitHubDeviceAuthorization
{
    public required string UserCode { get; init; }

    public required string VerificationUri { get; init; }

    public string? VerificationUriComplete { get; init; }

    public required int ExpiresInSeconds { get; init; }

    public required int IntervalSeconds { get; init; }

    /// <summary>
    /// Opaque device code used only for token polling. Do not display or log.
    /// </summary>
    public required string DeviceCode { get; init; }
}
