using System.Net;
using System.Text;
using PCL.Aurora.Application;

namespace PCL.Aurora.Application.Tests;

public sealed class GitHubLauncherUpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_ReleaseChannelIgnoresPrerelease()
    {
        var service = CreateService("""
            [
              {"tag_name":"v1.3.0-beta.1","name":"Beta","body":"beta","html_url":"https://github.com/Micro-ATP/PCL-Aurora/releases/tag/v1.3.0-beta.1","draft":false,"prerelease":true,"published_at":"2026-07-30T00:00:00Z"},
              {"tag_name":"v1.2.0","name":"Release","body":"stable","html_url":"https://github.com/Micro-ATP/PCL-Aurora/releases/tag/v1.2.0","draft":false,"prerelease":false,"published_at":"2026-07-29T00:00:00Z"}
            ]
            """);

        var result = await service.CheckAsync("1.2.0", LauncherUpdateChannel.Release);

        Assert.False(result.IsUpdateAvailable);
        Assert.Equal("1.2.0", result.LatestRelease.VersionName);
        Assert.Equal("PCL Aurora 1.2.0", result.LatestRelease.DisplayName);
    }

    [Fact]
    public async Task CheckAsync_BetaChannelSelectsHigherPrerelease()
    {
        var service = CreateService("""
            [
              {"tag_name":"v1.3.0-beta.2","name":"Beta","body":"# 新功能\n- 第一项\n- 第二项\n- 第三项\n- 第四项","html_url":"https://github.com/Micro-ATP/PCL-Aurora/releases/tag/v1.3.0-beta.2","draft":false,"prerelease":true,"published_at":"2026-07-30T00:00:00Z"},
              {"tag_name":"v1.2.0","name":"Release","body":"stable","html_url":"https://github.com/Micro-ATP/PCL-Aurora/releases/tag/v1.2.0","draft":false,"prerelease":false,"published_at":"2026-07-29T00:00:00Z"}
            ]
            """);

        var result = await service.CheckAsync("1.2.0", LauncherUpdateChannel.Beta);

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("1.3.0-beta.2", result.LatestRelease.VersionName);
        Assert.Equal("PCL Aurora 1.3.0 Beta 2", result.LatestRelease.DisplayName);
        Assert.Equal("新功能" + Environment.NewLine + "第一项" + Environment.NewLine + "第二项", result.LatestRelease.Summary);
    }

    [Fact]
    public async Task CheckAsync_ThrowsWhenNoEligibleReleaseExists()
    {
        var service = CreateService("""
            [
              {"tag_name":"v1.3.0-beta.1","name":"Beta","body":"beta","html_url":"https://github.com/Micro-ATP/PCL-Aurora/releases/tag/v1.3.0-beta.1","draft":false,"prerelease":true,"published_at":"2026-07-30T00:00:00Z"}
            ]
            """);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckAsync("1.2.0", LauncherUpdateChannel.Release));
    }

    [Fact]
    public async Task CheckAsync_ParsesHttpsReleaseAssets()
    {
        var service = CreateService("""
            [
              {
                "tag_name":"v1.4.0",
                "name":"Release",
                "body":"stable",
                "html_url":"https://github.com/Micro-ATP/PCL-Aurora/releases/tag/v1.4.0",
                "draft":false,
                "prerelease":false,
                "published_at":"2026-08-01T00:00:00Z",
                "assets":[
                  {"name":"PCL-Aurora-1.4.0-osx-arm64.zip","browser_download_url":"https://github.com/Micro-ATP/PCL-Aurora/releases/download/v1.4.0/PCL-Aurora-1.4.0-osx-arm64.zip","size":2048,"content_type":"application/zip"},
                  {"name":"SHA256SUMS","browser_download_url":"https://github.com/Micro-ATP/PCL-Aurora/releases/download/v1.4.0/SHA256SUMS","size":120,"content_type":"text/plain"},
                  {"name":"unsafe.zip","browser_download_url":"http://example.invalid/unsafe.zip","size":1,"content_type":"application/zip"}
                ]
              }
            ]
            """);

        var result = await service.CheckAsync("1.3.0", LauncherUpdateChannel.Release);

        Assert.True(result.IsUpdateAvailable);
        Assert.Collection(
            result.LatestRelease.Assets,
            asset => Assert.Equal("PCL-Aurora-1.4.0-osx-arm64.zip", asset.Name),
            asset => Assert.Equal("SHA256SUMS", asset.Name));
    }

    private static GitHubLauncherUpdateService CreateService(string responseJson) =>
        new(new HttpClient(new StaticResponseHandler(responseJson)));

    private sealed class StaticResponseHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
    }
}
