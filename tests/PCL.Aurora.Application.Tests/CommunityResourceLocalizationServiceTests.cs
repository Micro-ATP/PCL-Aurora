using System.Net;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class CommunityResourceLocalizationServiceTests
{
    [Fact]
    public void Localize_UsesBundledPclCeTranslationByModrinthSlug()
    {
        var service = new PclCeCommunityResourceLocalizationService();

        var localized = service.Localize(CreateProject("sodium", "Sodium"));

        Assert.True(localized.HasTranslatedTitle);
        Assert.Equal("钠", localized.DisplayTitle);
        Assert.Equal("  |  Sodium", localized.OriginalTitleDisplay);
    }

    [Fact]
    public async Task TranslateAsync_RequestsOnDemandAndCachesSuccessfulTranslation()
    {
        var handler = new TranslationHandler();
        using var client = new HttpClient(handler);
        var service = new PclCeCommunityResourceDescriptionTranslationService(client);
        var project = CreateProject("sodium", "Sodium");

        var first = await service.TranslateAsync(project);
        var second = await service.TranslateAsync(project);

        Assert.True(first.HasTranslation);
        Assert.Equal("一个现代化的渲染优化模组。", first.Translation);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("https://mod.mcimirror.top/translate/modrinth/project-id", handler.RequestUri?.AbsoluteUri);
    }

    private static CommunityResourceProject CreateProject(string slug, string title) => new(
        "project-id",
        slug,
        title,
        "A modern rendering optimization mod.",
        "author",
        CommunityResourceType.Mod,
        new Uri($"https://modrinth.com/mod/{slug}"),
        null,
        0,
        0,
        null,
        null,
        [],
        []);

    private sealed class TranslationHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"translated\":\"一个现代化的渲染优化模组。\"}"),
            });
        }
    }
}
