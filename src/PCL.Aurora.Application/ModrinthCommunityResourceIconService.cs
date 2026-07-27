namespace PCL.Aurora.Application;

public sealed class ModrinthCommunityResourceIconService(HttpClient httpClient) : ICommunityResourceIconService
{
    private const int MaximumIconBytes = 512 * 1024;
    private const int MaximumCachedIcons = 64;
    private const int MaximumConcurrentRequests = 6;
    private const string ModrinthCdnHost = "cdn.modrinth.com";

    private readonly object cacheGate = new();
    private readonly Dictionary<string, byte[]> cache = new(StringComparer.Ordinal);
    private readonly Queue<string> cacheOrder = new();
    private readonly SemaphoreSlim requestSlots = new(MaximumConcurrentRequests, MaximumConcurrentRequests);

    public async Task<byte[]?> LoadAsync(Uri iconUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(iconUrl);
        if (!IsAllowedIconUri(iconUrl))
        {
            return null;
        }

        var key = iconUrl.AbsoluteUri;
        if (TryGetCached(key, out var cached))
        {
            return cached;
        }

        await requestSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetCached(key, out cached))
            {
                return cached;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, iconUrl);
            request.Headers.UserAgent.ParseAdd("PCL-Aurora/0.1");
            request.Headers.Accept.ParseAdd("image/*");
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength is > MaximumIconBytes ||
                response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
            {
                return null;
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var destination = new MemoryStream();
            var buffer = new byte[16 * 1024];
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (destination.Length + read > MaximumIconBytes)
                {
                    return null;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            if (destination.Length == 0)
            {
                return null;
            }

            var bytes = destination.ToArray();
            AddToCache(key, bytes);
            return bytes;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return null;
        }
        finally
        {
            requestSlots.Release();
        }
    }

    private static bool IsAllowedIconUri(Uri iconUrl) =>
        iconUrl.IsAbsoluteUri &&
        iconUrl.Scheme == Uri.UriSchemeHttps &&
        string.Equals(iconUrl.Host, ModrinthCdnHost, StringComparison.OrdinalIgnoreCase);

    private bool TryGetCached(string key, out byte[]? bytes)
    {
        lock (cacheGate)
        {
            return cache.TryGetValue(key, out bytes);
        }
    }

    private void AddToCache(string key, byte[] bytes)
    {
        lock (cacheGate)
        {
            if (cache.ContainsKey(key))
            {
                return;
            }

            while (cache.Count >= MaximumCachedIcons && cacheOrder.TryDequeue(out var expired))
            {
                cache.Remove(expired);
            }

            cache.Add(key, bytes);
            cacheOrder.Enqueue(key);
        }
    }
}
