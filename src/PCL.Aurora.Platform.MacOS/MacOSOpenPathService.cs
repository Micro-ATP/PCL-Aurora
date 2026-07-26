using System.Diagnostics;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed class MacOSOpenPathService : IOpenPathService
{
    public async Task OpenFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await OpenAsync(path, "无法打开路径", cancellationToken).ConfigureAwait(false);
    }

    public Task OpenUriAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("只能在浏览器中打开 HTTP 或 HTTPS 地址。", nameof(uri));
        }

        return OpenAsync(uri.AbsoluteUri, "无法打开网页", cancellationToken);
    }

    private static async Task OpenAsync(string target, string errorPrefix, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add(target);
        process.Start();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{errorPrefix}：{target}");
        }
    }
}
