using FluentAssertions;
using GameSync.Core.Models;
using GameSync.Core.Services;

namespace GameSync.Core.Tests.Paths;

public sealed class FileTreeComparerTests
{
    [Fact]
    public void CompareDirectories_DetectsAddedChangedDeleted()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncTree", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(Path.Combine(source, "sub"));
        Directory.CreateDirectory(Path.Combine(target, "sub"));

        File.WriteAllText(Path.Combine(source, "new.txt"), "new");
        File.WriteAllText(Path.Combine(source, "sub", "changed.txt"), "v2");
        File.WriteAllText(Path.Combine(target, "sub", "changed.txt"), "v1");
        File.WriteAllText(Path.Combine(target, "gone.txt"), "bye");

        try
        {
            var result = FileTreeComparer.Compare(source, target, SaveLocationType.Directory);
            result.AddedFiles.Should().Contain("new.txt");
            result.ChangedFiles.Should().Contain("sub/changed.txt");
            result.DeletedFiles.Should().Contain("gone.txt");
            result.HasChanges.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CompareFiles_DetectsModification()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncFileCmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var left = Path.Combine(root, "a.dat");
        var right = Path.Combine(root, "b.dat");
        File.WriteAllText(left, "1");
        File.WriteAllText(right, "2");
        try
        {
            var result = FileTreeComparer.Compare(left, right, SaveLocationType.File);
            result.ChangedFiles.Should().ContainSingle().Which.Should().Be("a.dat");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CompareFiles_DetectsAdditionAndDeletion()
    {
        var root = Path.Combine(Path.GetTempPath(), "GameSyncFileCmp2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var left = Path.Combine(root, "only-local.dat");
        var right = Path.Combine(root, "missing.dat");
        File.WriteAllText(left, "1");
        try
        {
            FileTreeComparer.Compare(left, right, SaveLocationType.File).AddedFiles.Should().Contain("only-local.dat");
            FileTreeComparer.Compare(right, left, SaveLocationType.File).DeletedFiles.Should().Contain("missing.dat");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
