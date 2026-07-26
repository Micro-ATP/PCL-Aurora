using System.Globalization;
using System.Text.Json;

namespace PCL.Aurora.Domain;

public static class MinecraftVersionMetadataParser
{
    public static MinecraftVersionMetadataParseResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new(null, ["版本元数据为空。"]);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new(null, ["版本元数据根节点必须是对象。"]);
            }

            var id = GetString(root, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                return new(null, ["版本元数据缺少 id。"]);
            }

            var errors = new List<string>();
            var clientDownload = ParseClientDownload(root, errors);
            var assetIndex = ParseAssetIndex(root, errors);
            return new(
                new MinecraftVersionMetadata(
                    id,
                    GetString(root, "inheritsFrom"),
                    GetString(root, "type"),
                    ParseDateTime(GetString(root, "releaseTime")),
                    clientDownload,
                    assetIndex),
                errors);
        }
        catch (JsonException exception)
        {
            return new(null, [$"版本元数据不是有效 JSON：{exception.Message}"]);
        }
    }

    private static MinecraftVersionDownload? ParseClientDownload(JsonElement root, List<string> errors)
    {
        if (!TryGetObject(root, "downloads", out var downloads) || !TryGetObject(downloads, "client", out var client))
        {
            return null;
        }

        var url = ParseHttpUri(GetString(client, "url"), "客户端下载地址", errors);
        return url is null ? null : new(url, GetString(client, "sha1"), GetInt64(client, "size"));
    }

    private static MinecraftVersionAssetIndex? ParseAssetIndex(JsonElement root, List<string> errors)
    {
        if (!TryGetObject(root, "assetIndex", out var assetIndex))
        {
            return null;
        }

        var id = GetString(assetIndex, "id");
        var url = ParseHttpUri(GetString(assetIndex, "url"), "资源索引地址", errors);
        if (string.IsNullOrWhiteSpace(id))
        {
            errors.Add("资源索引缺少 id。");
        }

        return string.IsNullOrWhiteSpace(id) || url is null
            ? null
            : new MinecraftVersionAssetIndex(id, url, GetString(assetIndex, "sha1"), GetInt64(assetIndex, "size"));
    }

    private static Uri? ParseHttpUri(string? value, string fieldName, List<string> errors)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri;
        }

        errors.Add($"{fieldName}无效或缺失。");
        return null;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static long? GetInt64(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var value) && value >= 0
            ? value
            : null;

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement value) =>
        element.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.Object;

    private static DateTimeOffset? ParseDateTime(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;
}
