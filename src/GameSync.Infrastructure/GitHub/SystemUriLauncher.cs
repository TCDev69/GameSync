using System.Diagnostics;
using GameSync.Core.Abstractions;

namespace GameSync.Infrastructure.GitHub;

public sealed class SystemUriLauncher : IUriLauncher
{
    public Task OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only http/https URLs may be launched.", nameof(uri));
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }
}
