using System.Net;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class ModrinthCommunityResourceVersionServiceTests
{
    [Fact]
    public async Task GetProjectVersionsAsync_SendsGameVersionAndLoaderFilters()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new ModrinthCommunityResourceVersionService(client);

        var result = await service.GetProjectVersionsAsync(
            "fabric-api",
            "1.21.1",
            CommunityResourceLoader.Fabric);

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.RequestUri);
        var query = Uri.UnescapeDataString(handler.RequestUri!.Query);
        Assert.Contains("game_versions=[\"1.21.1\"]", query, StringComparison.Ordinal);
        Assert.Contains("loaders=[\"fabric\"]", query, StringComparison.Ordinal);
        Assert.Equal("PCL-Aurora/0.1", handler.UserAgent);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string UserAgent { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]"),
            });
        }
    }
}
