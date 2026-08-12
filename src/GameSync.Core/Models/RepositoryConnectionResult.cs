namespace GameSync.Core.Models;

public sealed class RepositoryConnectionResult
{
    public required bool Succeeded { get; init; }

    public required RepositoryConfiguration Repository { get; init; }

    public GamesConfiguration? Games { get; init; }

    public bool InitializedStructure { get; init; }

    public string? Message { get; init; }

    public Exception? Error { get; init; }

    public static RepositoryConnectionResult Success(
        RepositoryConfiguration repository,
        GamesConfiguration games,
        bool initializedStructure,
        string? message = null) =>
        new()
        {
            Succeeded = true,
            Repository = repository,
            Games = games,
            InitializedStructure = initializedStructure,
            Message = message
        };

    public static RepositoryConnectionResult Failure(
        RepositoryConfiguration repository,
        string message,
        Exception? error = null) =>
        new()
        {
            Succeeded = false,
            Repository = repository,
            Message = message,
            Error = error
        };
}
