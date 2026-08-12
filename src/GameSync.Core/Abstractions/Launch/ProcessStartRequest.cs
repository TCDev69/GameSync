namespace GameSync.Core.Abstractions.Launch;

public sealed class ProcessStartRequest
{
    public required string ExecutablePath { get; init; }

    public string Arguments { get; init; } = string.Empty;

    public string? WorkingDirectory { get; init; }
}
