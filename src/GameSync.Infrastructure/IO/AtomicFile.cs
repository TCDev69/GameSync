namespace GameSync.Infrastructure.IO;

/// <summary>
/// Copies files via a temporary sibling then atomic replace to avoid truncated destinations on crash.
/// </summary>
public static class AtomicFile
{
    public static void Copy(string sourceFile, string destinationFile, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFile);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = destinationFile + "." + Guid.NewGuid().ToString("N") + ".gamesync.tmp";
        try
        {
            File.Copy(sourceFile, temp, overwrite: true);
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(destinationFile))
            {
                File.Replace(temp, destinationFile, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, destinationFile);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch
                {
                    // Best-effort cleanup of temp leftovers.
                }
            }
        }
    }

    public static void WriteAllBytes(string destinationFile, byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFile);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = destinationFile + "." + Guid.NewGuid().ToString("N") + ".gamesync.tmp";
        try
        {
            File.WriteAllBytes(temp, content);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(destinationFile))
            {
                File.Replace(temp, destinationFile, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, destinationFile);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch
                {
                }
            }
        }
    }
}
