using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using PCL.Aurora.Application;

namespace PCL.Aurora.Infrastructure;

/// <summary>
/// 代理、DoH 和 Happy Eyeballs 结构适配自 PCL-CE NetworkService、DnsQuery 与 HostConnectionHandler。
/// </summary>
public sealed class LauncherNetworkSettingsService : ILauncherNetworkSettingsService, IWebProxy, IDisposable
{
    private static readonly TimeSpan DnsCacheDuration = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, CachedAddresses> dnsCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient dohClient;
    private LauncherMiscSettings settings = LauncherMiscSettings.Default;
    private string? customProxyPassword;
    private bool disposed;

    public LauncherNetworkSettingsService()
    {
        var dohHandler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            Proxy = this,
            UseProxy = true,
        };
        dohClient = new HttpClient(dohHandler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(8),
        };
        dohClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-json"));
    }

    public ICredentials? Credentials
    {
        get
        {
            var snapshot = Volatile.Read(ref settings);
            if (snapshot.ProxyMode != LauncherProxyMode.Custom || string.IsNullOrWhiteSpace(snapshot.CustomProxyUsername))
            {
                return null;
            }

            return new NetworkCredential(snapshot.CustomProxyUsername, Volatile.Read(ref customProxyPassword) ?? string.Empty);
        }
        set => throw new NotSupportedException("代理凭据由启动器安全设置管理。 ");
    }

    public void Apply(LauncherMiscSettings settings, string? customProxyPassword)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(settings));
        }

        Volatile.Write(ref this.settings, settings);
        Volatile.Write(ref this.customProxyPassword, customProxyPassword);
    }

    public HttpClient CreateHttpClient()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            MaxAutomaticRedirections = 20,
            Proxy = this,
            UseCookies = false,
            UseProxy = true,
            ConnectCallback = ConnectAsync,
        };
        return new HttpClient(handler, disposeHandler: true);
    }

    public Uri? GetProxy(Uri destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var snapshot = Volatile.Read(ref settings);
        return snapshot.ProxyMode switch
        {
            LauncherProxyMode.None => destination,
            LauncherProxyMode.System => HttpClient.DefaultProxy.GetProxy(destination),
            LauncherProxyMode.Custom when TryGetCustomProxy(snapshot, out var proxy) => proxy,
            _ => destination,
        };
    }

    public bool IsBypassed(Uri host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var snapshot = Volatile.Read(ref settings);
        return snapshot.ProxyMode switch
        {
            LauncherProxyMode.None => true,
            LauncherProxyMode.System => HttpClient.DefaultProxy.IsBypassed(host),
            LauncherProxyMode.Custom => !TryGetCustomProxy(snapshot, out _),
            _ => true,
        };
    }

    private async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var endPoint = context.DnsEndPoint;
        if (IPAddress.TryParse(endPoint.Host, out var directAddress))
        {
            return await ConnectAddressAsync(directAddress, endPoint.Port, cancellationToken).ConfigureAwait(false);
        }

        var snapshot = Volatile.Read(ref settings);
        if (!snapshot.EnableDoh)
        {
            return await ConnectSystemAsync(endPoint, cancellationToken).ConfigureAwait(false);
        }

        var addresses = await ResolveAddressesAsync(endPoint.Host, cancellationToken).ConfigureAwait(false);
        try
        {
            return await ConnectHappyEyeballsAsync(addresses, endPoint.Port, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
        {
            dnsCache.TryRemove(endPoint.Host, out _);
            return await ConnectSystemAsync(endPoint, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IPAddress[]> ResolveAddressesAsync(string host, CancellationToken cancellationToken)
    {
        if (dnsCache.TryGetValue(host, out var cached) && DateTimeOffset.UtcNow - cached.CreatedAt < DnsCacheDuration)
        {
            return cached.Addresses;
        }

        try
        {
            var responses = await Task.WhenAll(
                QueryDohAsync(host, "AAAA", cancellationToken),
                QueryDohAsync(host, "A", cancellationToken)).ConfigureAwait(false);
            var addresses = responses.SelectMany(static value => value).Distinct().ToArray();
            if (addresses.Length > 0)
            {
                dnsCache[host] = new CachedAddresses(DateTimeOffset.UtcNow, addresses);
                return addresses;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or TaskCanceledException)
        {
            if (exception is TaskCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        return await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<IPAddress>> QueryDohAsync(
        string host,
        string type,
        CancellationToken cancellationToken)
    {
        var uri = new Uri($"https://cloudflare-dns.com/dns-query?name={Uri.EscapeDataString(host)}&type={type}");
        using var response = await dohClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("Answer", out var answer) || answer.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var addresses = new List<IPAddress>();
        foreach (var item in answer.EnumerateArray())
        {
            if (item.TryGetProperty("data", out var data) &&
                IPAddress.TryParse(data.GetString(), out var address))
            {
                addresses.Add(address);
            }
        }

        return addresses;
    }

    private static async Task<Stream> ConnectHappyEyeballsAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        CancellationToken cancellationToken)
    {
        var candidates = addresses
            .OrderByDescending(static address => address.AddressFamily == AddressFamily.InterNetworkV6)
            .Take(4)
            .ToArray();
        if (candidates.Length == 0)
        {
            throw new HttpRequestException("DoH 未返回可用地址。 ");
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tasks = candidates.Select((address, index) =>
            ConnectDelayedAsync(address, port, index * (address.AddressFamily == AddressFamily.InterNetworkV6 ? 80 : 150), linked.Token)).ToList();
        var failures = new List<Exception>();
        while (tasks.Count > 0)
        {
            var completed = await Task.WhenAny(tasks).ConfigureAwait(false);
            tasks.Remove(completed);
            try
            {
                var stream = await completed.ConfigureAwait(false);
                linked.Cancel();
                foreach (var pending in tasks)
                {
                    _ = DisposeCompletedStreamAsync(pending);
                }
                return stream;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                failures.Add(exception);
            }
        }

        throw new HttpRequestException("无法连接 DoH 返回的地址。", new AggregateException(failures));
    }

    private static async Task DisposeCompletedStreamAsync(Task<Stream> task)
    {
        try
        {
            await using var stream = await task.ConfigureAwait(false);
        }
        catch
        {
            // The winning connection cancels and observes every remaining attempt.
        }
    }

    private static async Task<Stream> ConnectDelayedAsync(
        IPAddress address,
        int port,
        int delayMilliseconds,
        CancellationToken cancellationToken)
    {
        if (delayMilliseconds > 0)
        {
            await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        return await ConnectAddressAsync(address, port, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Stream> ConnectAddressAsync(
        IPAddress address,
        int port,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(address, port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async Task<Stream> ConnectSystemAsync(
        DnsEndPoint endPoint,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static bool TryGetCustomProxy(LauncherMiscSettings snapshot, out Uri proxy)
    {
        return Uri.TryCreate(snapshot.CustomProxyAddress, UriKind.Absolute, out proxy!) &&
               proxy.Scheme is "http" or "https";
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        dohClient.Dispose();
    }

    private sealed record CachedAddresses(DateTimeOffset CreatedAt, IPAddress[] Addresses);
}
