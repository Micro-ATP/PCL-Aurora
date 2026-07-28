// Directly adapts the on-demand description translation request from PCL-CE
// Plain Craft Launcher 2/Modules/Minecraft/ModComp.cs. Modified by Micro-ATP
// to use typed results, cancellation, HTTPS validation and an in-memory cache.
using System.Collections.Concurrent;
using System.Text.Json;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class PclCeCommunityResourceDescriptionTranslationService(HttpClient httpClient)
    : ICommunityResourceDescriptionTranslationService
{
    private static readonly Uri EndpointRoot = new("https://mod.mcimirror.top/translate/modrinth/");
    private readonly ConcurrentDictionary<string, string> cache = new(StringComparer.Ordinal);

    public async Task<CommunityResourceDescriptionTranslationResult> TranslateAsync(
        CommunityResourceProject project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (cache.TryGetValue(GetCacheKey(project), out var cached))
        {
            return CommunityResourceDescriptionTranslationResult.Success(cached);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(EndpointRoot, Uri.EscapeDataString(project.Id)));
            request.Headers.UserAgent.ParseAdd("PCL-Aurora/0.1");
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return CommunityResourceDescriptionTranslationResult.Failure("当前资源的简介暂无译文。");
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("translated", out var translatedValue) ||
                translatedValue.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(translatedValue.GetString()))
            {
                return CommunityResourceDescriptionTranslationResult.Failure("当前资源的简介暂无译文。");
            }

            var translation = translatedValue.GetString()!.Trim();
            cache[GetCacheKey(project)] = translation;
            return CommunityResourceDescriptionTranslationResult.Success(translation);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidOperationException)
        {
            return CommunityResourceDescriptionTranslationResult.Failure("获取简介译文失败，请稍后重试。");
        }
    }

    private static string GetCacheKey(CommunityResourceProject project) => $"{project.Id}\n{project.Description}";
}
