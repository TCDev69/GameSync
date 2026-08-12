namespace GameSync.Core.Models;

/// <summary>
/// Structured result of comparing configured game save locations to the repository copy.
/// </summary>
public sealed class SaveChangesDetected
{
    public static SaveChangesDetected Empty { get; } = new()
    {
        ChangedFiles = Array.Empty<string>(),
        AddedFiles = Array.Empty<string>(),
        DeletedFiles = Array.Empty<string>()
    };

    public IReadOnlyList<string> ChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AddedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DeletedFiles { get; init; } = Array.Empty<string>();

    public bool HasChanges => ChangedFiles.Count > 0 || AddedFiles.Count > 0 || DeletedFiles.Count > 0;

    public int TotalChanges => ChangedFiles.Count + AddedFiles.Count + DeletedFiles.Count;
}
