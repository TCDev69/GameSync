namespace GameSync.Core.Abstractions;

/// <summary>
/// Opens a URL with the system default handler (typically the browser).
/// </summary>
public interface IUriLauncher
{
    Task OpenAsync(Uri uri, CancellationToken cancellationToken = default);
}
