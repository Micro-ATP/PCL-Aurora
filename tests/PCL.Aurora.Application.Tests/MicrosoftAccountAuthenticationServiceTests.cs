using System.Net;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class MicrosoftAccountAuthenticationServiceTests
{
    [Fact]
    public async Task BeginAndCompleteDeviceCodeLogin_ExchangesTokensAndBuildsAuthenticatedAccount()
    {
        var handler = new SequenceHandler(
        [
            Json(HttpStatusCode.OK, """{"device_code":"opaque-device-code","user_code":"ABCD-EFGH","verification_uri":"https://microsoft.com/devicelogin","verification_uri_complete":"https://microsoft.com/devicelogin?code=ABCD-EFGH","expires_in":900,"interval":1}"""),
            Json(HttpStatusCode.OK, """{"access_token":"oauth-access","refresh_token":"refresh-token"}"""),
            Json(HttpStatusCode.OK, """{"Token":"xbl-token"}"""),
            Json(HttpStatusCode.OK, """{"Token":"xsts-token","DisplayClaims":{"xui":[{"uhs":"user-hash"}]}}"""),
            Json(HttpStatusCode.OK, """{"access_token":"minecraft-access"}"""),
            Json(HttpStatusCode.OK, """{"items":[{"name":"product_minecraft"}]}"""),
            Json(HttpStatusCode.OK, """{"id":"0123456789abcdef0123456789abcdef","name":"AuroraPlayer"}"""),
        ]);
        using var client = new HttpClient(handler);
        var service = new MicrosoftAccountAuthenticationService(client, new MicrosoftAuthenticationConfiguration("12345678-1234-1234-1234-1234567890ab"));
        var updates = new List<MicrosoftAuthenticationProgress>();

        var session = await service.BeginDeviceCodeLoginAsync();
        var result = await service.CompleteDeviceCodeLoginAsync(session, new InlineProgress<MicrosoftAuthenticationProgress>(updates.Add));

        Assert.Equal("ABCD-EFGH", session.Prompt.UserCode);
        Assert.Equal("https://microsoft.com/devicelogin?code=ABCD-EFGH", session.Prompt.OpenUri.AbsoluteUri);
        Assert.Equal("AuroraPlayer", result.Account.DisplayName);
        Assert.Equal("01234567-89ab-cdef-0123-456789abcdef", result.Account.Uuid);
        Assert.Equal(MinecraftAccountKind.Microsoft, result.Account.Kind);
        Assert.True(result.Account.IsAuthenticated);
        Assert.Equal("minecraft-access", result.Account.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
        Assert.Contains(updates, update => update.Description == "正在验证 Minecraft 所有权…");
        Assert.Equal(7, handler.RequestCount);
    }

    [Fact]
    public async Task BeginDeviceCodeLogin_RejectsMissingAuroraClientIdWithoutNetworkRequest()
    {
        var handler = new SequenceHandler([]);
        using var client = new HttpClient(handler);
        var service = new MicrosoftAccountAuthenticationService(client, new MicrosoftAuthenticationConfiguration(null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BeginDeviceCodeLoginAsync());

        Assert.Contains("Microsoft OAuth Client ID", exception.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public void Configuration_CanAcceptAValidatedPublicClientIdAtRuntime()
    {
        var configuration = new MicrosoftAuthenticationConfiguration(null);

        Assert.False(configuration.TrySetClientId("not-a-guid"));
        Assert.False(configuration.IsConfigured);
        Assert.True(configuration.TrySetClientId(" 12345678-1234-1234-1234-1234567890ab "));
        Assert.True(configuration.IsConfigured);
        Assert.Equal("12345678-1234-1234-1234-1234567890ab", configuration.ClientId);
    }

    [Fact]
    public async Task RefreshAsync_ReportsExpiredMicrosoftLoginWithoutLeakingToken()
    {
        var handler = new SequenceHandler([Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""")]);
        using var client = new HttpClient(handler);
        var service = new MicrosoftAccountAuthenticationService(client, new MicrosoftAuthenticationConfiguration("12345678-1234-1234-1234-1234567890ab"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshAsync("private-refresh-token"));

        Assert.Contains("重新进行设备代码登录", exception.Message);
        Assert.DoesNotContain("private-refresh-token", exception.Message);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string content) =>
        new(statusCode) { Content = new StringContent(content) };

    private sealed class SequenceHandler(IEnumerable<HttpResponseMessage> responseSequence) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new(responseSequence);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
