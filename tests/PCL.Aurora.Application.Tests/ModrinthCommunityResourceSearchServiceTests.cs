using System.Net;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class ModrinthCommunityResourceSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_UsesPublicModrinthFiltersAndPagination()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new ModrinthCommunityResourceSearchService(client);

        var result = await service.SearchAsync(new(
            CommunityResourceType.Mod,
            "sodium extra",
            "1.21.1",
            CommunityResourceLoader.Fabric,
            CommunityResourceSort.Downloads,
            2,
            20));

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.RequestUri);
        var query = Uri.UnescapeDataString(handler.RequestUri!.Query);
        Assert.Contains("query=sodium extra", query, StringComparison.Ordinal);
        Assert.Contains("index=downloads", query, StringComparison.Ordinal);
        Assert.Contains("offset=40", query, StringComparison.Ordinal);
        Assert.Contains("project_type:mod", query, StringComparison.Ordinal);
        Assert.Contains("versions:1.21.1", query, StringComparison.Ordinal);
        Assert.Contains("categories:fabric", query, StringComparison.Ordinal);
        Assert.Contains("PCL-Aurora/0.1", handler.UserAgent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_WorldDoesNotSendUnsupportedModrinthRequest()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new ModrinthCommunityResourceSearchService(client);

        var result = await service.SearchAsync(new(
            CommunityResourceType.World,
            string.Empty,
            null,
            CommunityResourceLoader.Any,
            CommunityResourceSort.Relevance,
            0));

        Assert.False(result.IsSuccess);
        Assert.Null(handler.RequestUri);
        Assert.Contains(result.Errors, error => error.Contains("不提供世界资源", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SearchAsync_MapsDataPacksToModrinthModCategoryIntersection()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new ModrinthCommunityResourceSearchService(client);

        var result = await service.SearchAsync(new(
            CommunityResourceType.DataPack,
            string.Empty,
            null,
            CommunityResourceLoader.Any,
            CommunityResourceSort.Relevance,
            0));

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.RequestUri);
        var query = Uri.UnescapeDataString(handler.RequestUri!.Query);
        Assert.Contains("project_type:mod", query, StringComparison.Ordinal);
        Assert.Contains("categories:datapack", query, StringComparison.Ordinal);
        Assert.DoesNotContain("project_type:datapack", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_RejectsPageOffsetOverflowWithoutSendingRequest()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new ModrinthCommunityResourceSearchService(client);

        var result = await service.SearchAsync(new(
            CommunityResourceType.Mod,
            string.Empty,
            null,
            CommunityResourceLoader.Any,
            CommunityResourceSort.Relevance,
            int.MaxValue,
            40));

        Assert.False(result.IsSuccess);
        Assert.Null(handler.RequestUri);
        Assert.Contains(result.Errors, error => error.Contains("页码", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SearchAsync_ReportsHttpFailure()
    {
        using var client = new HttpClient(new FailingHandler());
        var service = new ModrinthCommunityResourceSearchService(client);

        var result = await service.SearchAsync(new(
            CommunityResourceType.ResourcePack,
            string.Empty,
            null,
            CommunityResourceLoader.Any,
            CommunityResourceSort.Relevance,
            0));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("无法获取 Modrinth", StringComparison.Ordinal));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string UserAgent { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"hits\":[],\"offset\":40,\"limit\":20,\"total_hits\":0}"),
            });
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
    }
}
