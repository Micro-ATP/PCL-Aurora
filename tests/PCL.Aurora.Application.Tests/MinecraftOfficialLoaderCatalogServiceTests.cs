using System.Net;
using PCL.Aurora.Application;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftOfficialLoaderCatalogServiceTests
{
    [Fact]
    public async Task FetchAsync_UsesPclUpstreamPublicCatalogEndpointsAfterExplicitRequest()
    {
        using var client = new HttpClient(new OfficialCatalogHandler());
        var service = new MinecraftOfficialLoaderCatalogService(client);

        var result = await service.FetchAsync("1.20.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Catalog!.Entries.Count);
        Assert.Contains(result.Catalog.Entries, entry => entry.Kind == PCL.Aurora.Domain.MinecraftLoaderKind.OptiFine);
    }

    [Fact]
    public async Task FetchAsync_ReportsHttpFailure()
    {
        using var client = new HttpClient(new FailingHandler());
        var service = new MinecraftOfficialLoaderCatalogService(client);

        var result = await service.FetchAsync("1.20.1");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("无法获取", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FetchAsync_PreservesReachableOfficialSourcesWhenAnotherSourceFails()
    {
        using var client = new HttpClient(new PartialCatalogHandler());
        var service = new MinecraftOfficialLoaderCatalogService(client);

        var result = await service.FetchAsync("1.20.1");

        Assert.NotNull(result.Catalog);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Catalog!.Entries, entry => entry.Kind == PCL.Aurora.Domain.MinecraftLoaderKind.Forge);
        Assert.Contains(result.Catalog.Entries, entry => entry.Kind == PCL.Aurora.Domain.MinecraftLoaderKind.Fabric);
        Assert.Contains(result.Errors, error => error.Contains("NeoForge", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FetchAsync_WithLoaderKind_OnlyRequestsThatCatalog()
    {
        var handler = new RecordingCatalogHandler();
        using var client = new HttpClient(handler);
        var service = new MinecraftOfficialLoaderCatalogService(client);

        var result = await service.FetchAsync("1.20.1", PCL.Aurora.Domain.MinecraftLoaderKind.Forge);

        Assert.NotNull(result.Catalog);
        Assert.All(result.Catalog!.Entries, entry => Assert.Equal(PCL.Aurora.Domain.MinecraftLoaderKind.Forge, entry.Kind));
        Assert.Equal(["maven.minecraftforge.net"], handler.RequestedHosts);
    }

    private sealed class OfficialCatalogHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.RequestUri!.Host switch
            {
                "maven.minecraftforge.net" => """<metadata><versioning><versions><version>1.20.1-47.2.0</version></versions></versioning></metadata>""",
                "maven.neoforged.net" when request.RequestUri.AbsolutePath.EndsWith("/neoforge", StringComparison.Ordinal) => """{ "versions": ["20.1.1"] }""",
                "maven.neoforged.net" => """{ "versions": ["1.20.1-47.1.99"] }""",
                "meta.fabricmc.net" => """[{ "loader": { "version": "0.16.10", "stable": true } }]""",
                "bmclapi2.bangbang93.com" => """[{ "mcversion": "1.20.1", "type": "HD_U", "patch": "I6", "filename": "OptiFine_1.20.1_HD_U_I6.jar", "forge": "Forge 47.2.0" }]""",
                _ => throw new InvalidOperationException($"意外请求：{request.RequestUri}"),
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
    }

    private sealed class PartialCatalogHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "maven.neoforged.net")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
            }

            var content = request.RequestUri.Host switch
            {
                "maven.minecraftforge.net" => """<metadata><versioning><versions><version>1.20.1-47.2.0</version></versions></versioning></metadata>""",
                "bmclapi2.bangbang93.com" => """[{ "mcversion": "1.20.1", "type": "HD_U", "patch": "I6", "filename": "OptiFine_1.20.1_HD_U_I6.jar", "forge": "Forge 47.2.0" }]""",
                _ => """[{ "loader": { "version": "0.16.10", "stable": true } }]""",
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });
        }
    }

    private sealed class RecordingCatalogHandler : HttpMessageHandler
    {
        public List<string> RequestedHosts { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedHosts.Add(request.RequestUri!.Host);
            const string content = "<metadata><versioning><versions><version>1.20.1-47.2.0</version></versions></versioning></metadata>";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });
        }
    }
}
