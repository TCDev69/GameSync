using System.Security.Cryptography;
using GameSync.Core.Models;

namespace GameSync.Core.Services;

/// <summary>
/// Compares two directory trees (or single files) and reports added/changed/deleted paths
/// relative to the source root. Pure filesystem comparison — no Git knowledge.
/// </summary>
public static class FileTreeComparer
{
    public static SaveChangesDetected Compare(string sourcePath, string targetPath, SaveLocationType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        if (type == SaveLocationType.File)
        {
            return CompareFiles(sourcePath, targetPath);
        }

        return CompareDirectories(sourcePath, targetPath);
    }

    private static SaveChangesDetected CompareFiles(string sourceFile, string targetFile)
    {
        var sourceExists = File.Exists(sourceFile);
        var targetExists = File.Exists(targetFile);
        var name = Path.GetFileName(sourceFile);

        if (sourceExists && !targetExists)
        {
            return new SaveChangesDetected { AddedFiles = [name] };
        }

        if (!sourceExists && targetExists)
        {
            return new SaveChangesDetected { DeletedFiles = [name] };
        }

        if (sourceExists && targetExists && !FilesEqual(sourceFile, targetFile))
        {
            return new SaveChangesDetected { ChangedFiles = [name] };
        }

        return SaveChangesDetected.Empty;
    }

    private static SaveChangesDetected CompareDirectories(string sourceDir, string targetDir)
    {
        var added = new List<string>();
        var changed = new List<string>();
        var deleted = new List<string>();

        var sourceFiles = EnumerateRelativeFiles(sourceDir);
        var targetFiles = EnumerateRelativeFiles(targetDir);

        foreach (var relative in sourceFiles)
        {
            if (!targetFiles.Contains(relative))
            {
                added.Add(relative);
                continue;
            }

            var sourceFile = Path.Combine(sourceDir, relative.Replace('/', Path.DirectorySeparatorChar));
            var targetFile = Path.Combine(targetDir, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!FilesEqual(sourceFile, targetFile))
            {
                changed.Add(relative);
            }
        }

        foreach (var relative in targetFiles)
        {
            if (!sourceFiles.Contains(relative))
            {
                deleted.Add(relative);
            }
        }

        return new SaveChangesDetected
        {
            AddedFiles = added.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            ChangedFiles = changed.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            DeletedFiles = deleted.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    public static HashSet<string> EnumerateRelativeFiles(string directory)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory))
        {
            return set;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            set.Add(relative);
        }

        return set;
    }

    public static bool FilesEqual(string left, string right)
    {
        var leftInfo = new FileInfo(left);
        var rightInfo = new FileInfo(right);
        if (leftInfo.Length != rightInfo.Length)
        {
            return false;
        }

        // Fast path only when both size and last-write match still requires hashing for save safety.
        // Large-file mtime equality alone previously skipped hashing and could miss edits.
        using var leftStream = File.OpenRead(left);
        using var rightStream = File.OpenRead(right);
        var leftHash = SHA256.HashData(leftStream);
        var rightHash = SHA256.HashData(rightStream);
        return leftHash.AsSpan().SequenceEqual(rightHash);
    }
}
