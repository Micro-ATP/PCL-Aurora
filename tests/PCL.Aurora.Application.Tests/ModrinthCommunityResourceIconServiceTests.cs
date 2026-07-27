using System.Net;
using System.Net.Http.Headers;
using PCL.Aurora.Application;

namespace PCL.Aurora.Application.Tests;

public sealed class ModrinthCommunityResourceIconServiceTests
{
    private static readonly Uri TrustedIconUri = new("https://cdn.modrinth.com/data/example/icon.png");

    [Fact]
    public async Task LoadAsync_DownloadsAndCachesTrustedImage()
    {
        var handler = new IconHandler(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        using var client = new HttpClient(handler);
        var service = new ModrinthCommunityResourceIconService(client);

        var first = await service.LoadAsync(TrustedIconUri);
        var second = await service.LoadAsync(TrustedIconUri);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("PCL-Aurora/0.1", handler.UserAgent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_RejectsUnexpectedHostWithoutSendingRequest()
    {
        var handler = new IconHandler([1, 2, 3]);
        using var client = new HttpClient(handler);
        var service = new ModrinthCommunityResourceIconService(client);

        var result = await service.LoadAsync(new Uri("https://example.com/icon.png"));

        Assert.Null(result);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task LoadAsync_RejectsOversizedImage()
    {
        var handler = new IconHandler(new byte[512 * 1024 + 1]);
        using var client = new HttpClient(handler);
        var service = new ModrinthCommunityResourceIconService(client);

        var result = await service.LoadAsync(TrustedIconUri);

        Assert.Null(result);
        Assert.Equal(1, handler.RequestCount);
    }

    private sealed class IconHandler(byte[] content) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public string UserAgent { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            UserAgent = request.Headers.UserAgent.ToString();
            var responseContent = new ByteArrayContent(content);
            responseContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = responseContent,
            });
        }
    }
}
