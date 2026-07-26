// Directly adapted from PCL-CE, Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.cs
// and Plain Craft Launcher 2/Pages/PageLaunch/MyMsgLogin.xaml.cs.
// Copyright © 成都瓜皮龙科技有限公司.
// Modified by Micro-ATP for PCL Aurora: rebuilds the protocol flow with cross-platform
// HttpClient and Keychain-bound session storage; omits PCL-CE credentials, WPF UI, config and logging.
// See LICENSES/PCL-CE-Plain-Craft-Launcher-2-LICENCE.txt and NOTICE.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

/// <summary>
/// Microsoft 设备代码登录与 Minecraft 服务令牌交换。
///
/// 协议顺序、端点和 Xbox/Minecraft 所有权检查直接适配自 PCL-CE 的
/// Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.cs 与
/// Plain Craft Launcher 2/Pages/PageLaunch/MyMsgLogin.xaml.cs。
/// 本实现不使用其 Client ID、WPF 对话框、配置或日志，并且不会记录任何令牌。
/// </summary>
public sealed class MicrosoftAccountAuthenticationService(
    HttpClient httpClient,
    MicrosoftAuthenticationConfiguration configuration) : IMicrosoftAccountAuthenticationService
{
    private static readonly Uri DeviceCodeUri = new("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode");
    private static readonly Uri DeviceTokenUri = new("https://login.microsoftonline.com/consumers/oauth2/v2.0/token");
    private static readonly Uri RefreshTokenUri = new("https://login.live.com/oauth20_token.srf");
    private static readonly Uri XboxLiveAuthenticationUri = new("https://user.auth.xboxlive.com/user/authenticate");
    private static readonly Uri XstsAuthorizationUri = new("https://xsts.auth.xboxlive.com/xsts/authorize");
    private static readonly Uri MinecraftLoginUri = new("https://api.minecraftservices.com/authentication/login_with_xbox");
    private static readonly Uri MinecraftEntitlementsUri = new("https://api.minecraftservices.com/entitlements/mcstore");
    private static readonly Uri MinecraftProfileUri = new("https://api.minecraftservices.com/minecraft/profile");
    private const string Scope = "XboxLive.signin offline_access";

    public bool IsConfigured => configuration.IsConfigured;

    public async Task<MicrosoftDeviceCodeSession> BeginDeviceCodeLoginAsync(CancellationToken cancellationToken = default)
    {
        configuration.EnsureConfigured();
        using var request = new HttpRequestMessage(HttpMethod.Post, DeviceCodeUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = configuration.ClientId!,
                ["scope"] = Scope,
            }),
        };
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, document, "无法开始 Microsoft 设备代码登录。");

        var deviceCode = GetRequiredString(document.RootElement, "device_code", "设备代码响应缺少 device_code。");
        var userCode = GetRequiredString(document.RootElement, "user_code", "设备代码响应缺少 user_code。");
        var verificationUri = GetRequiredUri(document.RootElement, "verification_uri", "设备代码响应缺少 verification_uri。");
        var openUri = GetOptionalUri(document.RootElement, "verification_uri_complete") ?? verificationUri;
        var expiresIn = GetRequiredPositiveInt(document.RootElement, "expires_in", "设备代码响应缺少有效 expires_in。");
        var interval = GetOptionalPositiveInt(document.RootElement, "interval") ?? 5;
        return new MicrosoftDeviceCodeSession(
            deviceCode,
            TimeSpan.FromSeconds(interval),
            new MicrosoftDeviceCodePrompt(userCode, verificationUri, openUri, DateTimeOffset.UtcNow.AddSeconds(expiresIn)));
    }

    public async Task<MicrosoftAuthenticationResult> CompleteDeviceCodeLoginAsync(
        MicrosoftDeviceCodeSession session,
        IProgress<MicrosoftAuthenticationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        configuration.EnsureConfigured();
        var interval = session.PollInterval;
        progress?.Report(new("等待 Microsoft 账户完成授权…"));
        while (DateTimeOffset.UtcNow < session.Prompt.ExpiresAt)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Post, DeviceTokenUri)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["client_id"] = configuration.ClientId!,
                    ["device_code"] = session.DeviceCode,
                    ["scope"] = Scope,
                }),
            };
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var accessToken = GetRequiredString(document.RootElement, "access_token", "OAuth 响应缺少 access_token。");
                var refreshToken = GetRequiredString(document.RootElement, "refresh_token", "OAuth 响应缺少 refresh_token。");
                return await ExchangeTokensAsync(accessToken, refreshToken, progress, cancellationToken).ConfigureAwait(false);
            }

            var error = GetOptionalString(document.RootElement, "error");
            switch (error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    continue;
                case "authorization_declined":
                    throw new InvalidOperationException("Microsoft 账户拒绝了授权请求。");
                case "expired_token":
                case "bad_verification_code":
                    throw new InvalidOperationException("Microsoft 设备代码已失效，请重新开始登录。");
                default:
                    throw CreateAuthenticationException(response.StatusCode, error, "Microsoft 设备代码轮询失败。");
            }
        }

        throw new InvalidOperationException("Microsoft 设备代码已过期，请重新开始登录。");
    }

    public async Task<MicrosoftAuthenticationResult> RefreshAsync(
        string refreshToken,
        IProgress<MicrosoftAuthenticationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        configuration.EnsureConfigured();
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("刷新令牌不能为空。", nameof(refreshToken));
        }

        progress?.Report(new("正在刷新 Microsoft 授权…"));
        using var request = new HttpRequestMessage(HttpMethod.Post, RefreshTokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = configuration.ClientId!,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
                ["scope"] = Scope,
            }),
        };
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = GetOptionalString(document.RootElement, "error");
            if (error is "invalid_grant" or "interaction_required")
            {
                throw new InvalidOperationException("Microsoft 登录已失效，请重新进行设备代码登录。");
            }

            throw CreateAuthenticationException(response.StatusCode, error, "无法刷新 Microsoft 授权。");
        }

        var accessToken = GetRequiredString(document.RootElement, "access_token", "刷新响应缺少 access_token。");
        var newRefreshToken = GetRequiredString(document.RootElement, "refresh_token", "刷新响应缺少 refresh_token。");
        return await ExchangeTokensAsync(accessToken, newRefreshToken, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MicrosoftAuthenticationResult> ExchangeTokensAsync(
        string oauthAccessToken,
        string refreshToken,
        IProgress<MicrosoftAuthenticationProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new("正在验证 Xbox Live 身份…"));
        var xblDocument = await PostJsonAsync(
            XboxLiveAuthenticationUri,
            new
            {
                Properties = new
                {
                    AuthMethod = "RPS",
                    SiteName = "user.auth.xboxlive.com",
                    RpsTicket = $"d={oauthAccessToken}",
                },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT",
            },
            cancellationToken).ConfigureAwait(false);
        var xblToken = GetRequiredString(xblDocument.RootElement, "Token", "Xbox Live 响应缺少令牌。");

        progress?.Report(new("正在获取 Xbox 安全令牌…"));
        var xstsDocument = await PostJsonAsync(
            XstsAuthorizationUri,
            new
            {
                Properties = new
                {
                    SandboxId = "RETAIL",
                    UserTokens = new[] { xblToken },
                },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT",
            },
            cancellationToken).ConfigureAwait(false);
        var xstsToken = GetRequiredString(xstsDocument.RootElement, "Token", "XSTS 响应缺少令牌。");
        var uhs = GetRequiredString(
            xstsDocument.RootElement,
            "DisplayClaims.xui[0].uhs",
            "XSTS 响应缺少用户哈希。");

        progress?.Report(new("正在获取 Minecraft 访问令牌…"));
        var minecraftTokenDocument = await PostJsonAsync(
            MinecraftLoginUri,
            new { identityToken = $"XBL3.0 x={uhs};{xstsToken}" },
            cancellationToken).ConfigureAwait(false);
        var minecraftAccessToken = GetRequiredString(minecraftTokenDocument.RootElement, "access_token", "Minecraft 响应缺少访问令牌。");

        progress?.Report(new("正在验证 Minecraft 所有权…"));
        var entitlementsDocument = await GetAuthenticatedJsonAsync(MinecraftEntitlementsUri, minecraftAccessToken, cancellationToken).ConfigureAwait(false);
        if (!HasMinecraftEntitlement(entitlementsDocument.RootElement))
        {
            throw new InvalidOperationException("此 Microsoft 账户未检测到 Minecraft Java 版所有权。");
        }

        progress?.Report(new("正在读取 Minecraft 玩家档案…"));
        var profileDocument = await GetAuthenticatedJsonAsync(MinecraftProfileUri, minecraftAccessToken, cancellationToken).ConfigureAwait(false);
        var displayName = GetRequiredString(profileDocument.RootElement, "name", "Minecraft 档案缺少名称。");
        var uuid = NormalizeMinecraftUuid(GetRequiredString(profileDocument.RootElement, "id", "Minecraft 档案缺少 UUID。"));
        progress?.Report(new("Microsoft 账户认证完成。"));
        return new(
            new MinecraftAccount(displayName, uuid, MinecraftAccountKind.Microsoft, true)
            {
                AccessToken = minecraftAccessToken,
            },
            refreshToken);
    }

    private async Task<JsonDocument> PostJsonAsync(Uri uri, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload),
        };
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, document, $"认证服务请求失败：{uri.Host}。");
        return document;
    }

    private async Task<JsonDocument> GetAuthenticatedJsonAsync(Uri uri, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, document, $"认证服务请求失败：{uri.Host}。");
        return document;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(content) ? "{}" : content);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("认证服务返回了无效 JSON。", exception);
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, JsonDocument document, string message)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw CreateAuthenticationException(response.StatusCode, GetOptionalString(document.RootElement, "error"), message);
        }
    }

    private static InvalidOperationException CreateAuthenticationException(HttpStatusCode statusCode, string? error, string message) =>
        new($"{message} HTTP {(int)statusCode}{(string.IsNullOrWhiteSpace(error) ? string.Empty : $"（{error}）")}。");

    private static bool HasMinecraftEntitlement(JsonElement root)
    {
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return items.EnumerateArray().Any(item =>
            string.Equals(GetOptionalString(item, "name"), "product_minecraft", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetOptionalString(item, "name"), "game_minecraft", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeMinecraftUuid(string value)
    {
        if (Guid.TryParseExact(value, "N", out var uuid) || Guid.TryParse(value, out uuid))
        {
            return uuid.ToString("D");
        }

        throw new InvalidDataException("Minecraft 档案 UUID 无效。");
    }

    private static string GetRequiredString(JsonElement root, string path, string errorMessage) =>
        GetOptionalString(root, path) is { Length: > 0 } value ? value : throw new InvalidDataException(errorMessage);

    private static string? GetOptionalString(JsonElement root, string path)
    {
        var element = root;
        foreach (var segment in path.Split('.'))
        {
            var name = segment;
            int? arrayIndex = null;
            var bracket = segment.IndexOf('[');
            if (bracket >= 0)
            {
                name = segment[..bracket];
                var closing = segment.IndexOf(']', bracket);
                if (closing <= bracket || !int.TryParse(segment[(bracket + 1)..closing], out var index))
                {
                    return null;
                }

                arrayIndex = index;
            }

            if (!element.TryGetProperty(name, out element))
            {
                return null;
            }

            if (arrayIndex is { } targetIndex)
            {
                if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() <= targetIndex)
                {
                    return null;
                }

                element = element[targetIndex];
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static Uri GetRequiredUri(JsonElement root, string name, string errorMessage) =>
        GetOptionalUri(root, name) ?? throw new InvalidDataException(errorMessage);

    private static Uri? GetOptionalUri(JsonElement root, string name) =>
        Uri.TryCreate(GetOptionalString(root, name), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri : null;

    private static int GetRequiredPositiveInt(JsonElement root, string name, string errorMessage) =>
        GetOptionalPositiveInt(root, name) ?? throw new InvalidDataException(errorMessage);

    private static int? GetOptionalPositiveInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element) && element.TryGetInt32(out var value) && value > 0 ? value : null;
}
