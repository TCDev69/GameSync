using FluentAssertions;
using GameSync.Core.Services;
using GameSync.Infrastructure.IO;

namespace GameSync.Infrastructure.Tests.IO;

public sealed class AtomicFileAndComparerTests
{
    [Fact]
    public void AtomicFile_Copy_ReplacesDestinationWithoutLeavingPartial()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncAtomic", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "src.bin");
            var dest = Path.Combine(root, "dest.bin");
            File.WriteAllText(source, "hello-world");
            File.WriteAllText(dest, "old");

            AtomicFile.Copy(source, dest);

            File.ReadAllText(dest).Should().Be("hello-world");
            Directory.GetFiles(root, "*.gamesync.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FilesEqual_DetectsContentChangeEvenWhenSizeMatches()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncHash", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var left = Path.Combine(root, "a.bin");
            var right = Path.Combine(root, "b.bin");
            // Same length, different content (previously large-file mtime fast-path could miss this).
            File.WriteAllBytes(left, Enumerable.Repeat((byte)1, 2 * 1024 * 1024).ToArray());
            File.WriteAllBytes(right, Enumerable.Repeat((byte)2, 2 * 1024 * 1024).ToArray());
            File.SetLastWriteTimeUtc(left, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(right, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            FileTreeComparer.FilesEqual(left, right).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
