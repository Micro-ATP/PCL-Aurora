using System.Net;
using System.Text.Json;
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
            Json(HttpStatusCode.OK, """{"access_token":"d=oauth-access","refresh_token":"refresh-token"}"""),
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
        var xboxRequest = handler.Requests[2];
        using var xboxPayload = JsonDocument.Parse(xboxRequest.Content!);
        Assert.Equal("d=oauth-access", xboxPayload.RootElement.GetProperty("Properties").GetProperty("RpsTicket").GetString());
        Assert.Equal("application/json", xboxRequest.ContentType);
        Assert.Contains("application/json", xboxRequest.Accept);
        Assert.Equal("1", xboxRequest.XboxContractVersion);
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
    public void Configuration_OnlyAcceptsAValidatedBuildOrEnvironmentClientId()
    {
        var invalidConfiguration = new MicrosoftAuthenticationConfiguration("not-a-guid");
        var configuration = new MicrosoftAuthenticationConfiguration(" 12345678-1234-1234-1234-1234567890ab ");

        Assert.False(invalidConfiguration.IsConfigured);
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

    [Fact]
    public async Task CompleteDeviceCodeLogin_ReportsXboxErrorWithoutLeakingOAuthTokenOrServiceMessage()
    {
        var handler = new SequenceHandler(
        [
            Json(HttpStatusCode.OK, """{"device_code":"opaque-device-code","user_code":"ABCD-EFGH","verification_uri":"https://microsoft.com/devicelogin","expires_in":900,"interval":1}"""),
            Json(HttpStatusCode.OK, """{"access_token":"private-oauth-access","refresh_token":"private-refresh-token"}"""),
            Json(HttpStatusCode.BadRequest, """{"Identity":"0","XErr":2148916233,"Message":"private-oauth-access","Redirect":"https://start.ui.xboxlive.com/"}"""),
        ]);
        using var client = new HttpClient(handler);
        var service = new MicrosoftAccountAuthenticationService(client, new MicrosoftAuthenticationConfiguration("12345678-1234-1234-1234-1234567890ab"));

        var session = await service.BeginDeviceCodeLoginAsync();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteDeviceCodeLoginAsync(session));

        Assert.Contains("尚未注册 Xbox 档案", exception.Message);
        Assert.Contains("2148916233", exception.Message);
        Assert.DoesNotContain("private-oauth-access", exception.Message);
        Assert.DoesNotContain("private-refresh-token", exception.Message);
    }

    [Fact]
    public async Task CompleteDeviceCodeLogin_ExplainsMinecraftAppRegistrationForbiddenWithoutLeakingTokens()
    {
        var handler = new SequenceHandler(
        [
            Json(HttpStatusCode.OK, """{"device_code":"opaque-device-code","user_code":"ABCD-EFGH","verification_uri":"https://microsoft.com/devicelogin","expires_in":900,"interval":1}"""),
            Json(HttpStatusCode.OK, """{"access_token":"private-oauth-access","refresh_token":"private-refresh-token"}"""),
            Json(HttpStatusCode.OK, """{"Token":"private-xbl-token"}"""),
            Json(HttpStatusCode.OK, """{"Token":"private-xsts-token","DisplayClaims":{"xui":[{"uhs":"private-user-hash"}]}}"""),
            Json(HttpStatusCode.Forbidden, """{"errorType":"ForbiddenOperationException","errorMessage":"Invalid app registration: private-xsts-token"}"""),
        ]);
        using var client = new HttpClient(handler);
        var service = new MicrosoftAccountAuthenticationService(client, new MicrosoftAuthenticationConfiguration("12345678-1234-1234-1234-1234567890ab"));

        var session = await service.BeginDeviceCodeLoginAsync();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteDeviceCodeLoginAsync(session));

        Assert.Contains("Client ID", exception.Message);
        Assert.Contains("官方 AppID 审核", exception.Message);
        Assert.Contains("HTTP 403", exception.Message);
        Assert.DoesNotContain("private-oauth-access", exception.Message);
        Assert.DoesNotContain("private-refresh-token", exception.Message);
        Assert.DoesNotContain("private-xbl-token", exception.Message);
        Assert.DoesNotContain("private-xsts-token", exception.Message);
        Assert.DoesNotContain("private-user-hash", exception.Message);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string content) =>
        new(statusCode) { Content = new StringContent(content) };

    private sealed class SequenceHandler(IEnumerable<HttpResponseMessage> responseSequence) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new(responseSequence);

        public int RequestCount { get; private set; }
        public List<RequestSnapshot> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Requests.Add(new(
                request.Method,
                request.RequestUri,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Content?.Headers.ContentType?.MediaType,
                request.Headers.Accept.Select(value => value.MediaType ?? string.Empty).ToArray(),
                request.Headers.TryGetValues("x-xbl-contract-version", out var values) ? values.SingleOrDefault() : null));
            return responses.Dequeue();
        }
    }

    private sealed record RequestSnapshot(
        HttpMethod Method,
        Uri? Uri,
        string? Content,
        string? ContentType,
        IReadOnlyList<string> Accept,
        string? XboxContractVersion);

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
