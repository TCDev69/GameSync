namespace GameSync.Core.Abstractions;

/// <summary>
/// Resolves path templates containing Windows environment variables and enforces repository path safety.
/// </summary>
public interface IPathResolver
{
    /// <summary>
    /// Expands environment variables (e.g. %USERPROFILE%) and normalizes to a full Windows path.
    /// </summary>
    string Resolve(string pathTemplate);

    /// <summary>
    /// Returns true when the template contains known expandable tokens or is otherwise usable.
    /// </summary>
    bool IsValidTemplate(string pathTemplate);

    /// <summary>
    /// Normalizes separators and resolves "." segments without requiring the path to exist.
    /// </summary>
    string Normalize(string path);

    /// <summary>
    /// Validates that a remote repository-relative path is safe (not rooted, no traversal).
    /// </summary>
    bool IsSafeRemotePath(string remotePath);

    /// <summary>
    /// Maps a remote repository-relative path to an absolute path under <paramref name="repositoryRoot"/>.
    /// Throws if the result would escape the repository root.
    /// </summary>
    string MapRemotePathToRepository(string repositoryRoot, string remotePath);

    /// <summary>
    /// Returns true when a resolved absolute local path is acceptable as a save target
    /// (blocks system folders, SSH keys, and GameSync-managed storage).
    /// </summary>
    bool IsAllowedLocalSaveTarget(string resolvedAbsolutePath);

    /// <summary>
    /// Converts an absolute Windows path into a portable template using known environment
    /// variables when possible (e.g. C:\Users\Alice\... → %USERPROFILE%/...).
    /// Paths that already contain %tokens% are normalized to forward slashes.
    /// </summary>
    string ToPortableTemplate(string absoluteOrTemplatePath);

    /// <summary>
    /// Returns the repository-relative path (forward slashes) for an absolute path under the repo root.
    /// </summary>
    string GetRepositoryRelativePath(string repositoryRoot, string absolutePath);
}
