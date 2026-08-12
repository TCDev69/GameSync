namespace GameSync.Core.Errors;

/// <summary>
/// Base exception for domain-level GameSync failures surfaced to the UI.
/// </summary>
public class GameSyncException : Exception
{
    public string ErrorCode { get; }

    public GameSyncException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public GameSyncException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
