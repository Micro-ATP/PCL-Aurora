using System.Net;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class CurseForgeCommunityResourceSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_UsesPclCeWorldClassCategorySortAndPagination()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new CurseForgeCommunityResourceSearchService(client);

        var result = await service.SearchAsync(new(
            CommunityResourceType.World,
            "one block",
            "1.21.1",
            CommunityResourceLoader.Any,
            CommunityResourceSort.Downloads,
            2,
            20,
            "253"));

        Assert.True(result.IsSuccess);
        var query = Uri.UnescapeDataString(handler.RequestUri!.Query);
        Assert.Contains("gameId=432", query, StringComparison.Ordinal);
        Assert.Contains("classId=17", query, StringComparison.Ordinal);
        Assert.Contains("categoryId=253", query, StringComparison.Ordinal);
        Assert.Contains("sortField=6", query, StringComparison.Ordinal);
        Assert.Contains("index=40", query, StringComparison.Ordinal);
        Assert.Contains("searchFilter=one block", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_RejectsUnknownWorldCategoryWithoutRequest()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new CurseForgeCommunityResourceSearchService(client);

        var result = await service.SearchAsync(new(
            CommunityResourceType.World,
            string.Empty,
            null,
            CommunityResourceLoader.Any,
            CommunityResourceSort.Default,
            0,
            Category: "999"));

        Assert.False(result.IsSuccess);
        Assert.Null(handler.RequestUri);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"data\":[],\"pagination\":{\"index\":40,\"pageSize\":20,\"totalCount\":0}}"),
            });
        }
    }
}
