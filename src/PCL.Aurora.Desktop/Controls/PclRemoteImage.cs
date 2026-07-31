using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace PCL.Aurora.Desktop.Controls;

internal sealed class PclRemoteImage : Border
{
    private const int MaximumImageBytes = 8 * 1024 * 1024;
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(10),
    })
    {
        Timeout = TimeSpan.FromSeconds(25),
    };
    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> Cache = new(StringComparer.Ordinal);

    public PclRemoteImage(string source)
    {
        MinHeight = 54;
        MaxWidth = 760;
        Margin = new Thickness(40, 8, 40, 8);
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        Background = new SolidColorBrush(Color.Parse("#ECF3FA"));
        CornerRadius = new CornerRadius(4);
        Child = new TextBlock
        {
            Margin = new Thickness(12),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#83909E")),
            Text = "正在加载图片",
        };
        _ = LoadAsync(source);
    }

    private async Task LoadAsync(string source)
    {
        var normalized = NormalizeSource(source);
        var bitmap = normalized is null
            ? null
            : await Cache.GetOrAdd(normalized.AbsoluteUri, _ => DownloadAsync(normalized));
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (bitmap is null)
            {
                Child = new TextBlock
                {
                    Margin = new Thickness(12),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.Parse("#83909E")),
                    Text = "图片暂时无法加载",
                };
                return;
            }

            Background = Brushes.Transparent;
            Child = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                MaxWidth = 760,
            };
        });
    }

    private static Uri? NormalizeSource(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        if (uri.Scheme == "http")
        {
            var builder = new UriBuilder(uri) { Scheme = "https", Port = -1 };
            uri = builder.Uri;
        }

        return uri;
    }

    private static async Task<Bitmap?> DownloadAsync(Uri uri)
    {
        try
        {
            using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumImageBytes)
            {
                return null;
            }

            await using var source = await response.Content.ReadAsStreamAsync();
            using var buffer = new MemoryStream();
            var bytes = new byte[81920];
            var total = 0;
            while (true)
            {
                var read = await source.ReadAsync(bytes);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > MaximumImageBytes)
                {
                    return null;
                }
                buffer.Write(bytes, 0, read);
            }

            buffer.Position = 0;
            return Bitmap.DecodeToWidth(buffer, 900);
        }
        catch
        {
            return null;
        }
    }
}
