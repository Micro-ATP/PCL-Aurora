using System.Globalization;
using System.Text.Json;

namespace PCL.Aurora.Domain;

public static class MinecraftVersionCatalogParser
{
    public static MinecraftVersionCatalogParseResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new(null, ["版本清单为空。"]);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("versions", out var versions) || versions.ValueKind != JsonValueKind.Array)
            {
                return new(null, ["版本清单缺少 versions 数组。"]);
            }

            var errors = new List<string>();
            var entries = new List<MinecraftVersionCatalogEntry>();
            foreach (var version in versions.EnumerateArray())
            {
                if (version.ValueKind != JsonValueKind.Object)
                {
                    errors.Add("版本清单包含非对象条目。");
                    continue;
                }

                var id = GetString(version, "id");
                var type = GetString(version, "type");
                var url = GetHttpUri(version, "url");
                var releaseTime = GetDateTime(version, "releaseTime");
                if (!IsSafeVersionId(id) || string.IsNullOrWhiteSpace(type) || url is null || releaseTime is null)
                {
                    errors.Add($"版本清单包含无效条目：{id ?? "未知版本"}。 ");
                    continue;
                }

                entries.Add(new(id!, type!, url, releaseTime.Value));
            }

            if (entries.Count == 0)
            {
                errors.Add("版本清单不包含有效版本。 ");
            }

            var latestRelease = GetNestedString(root, "latest", "release");
            var latestSnapshot = GetNestedString(root, "latest", "snapshot");
            return errors.Count > 0
                ? new(null, errors)
                : new(new(latestRelease, latestSnapshot, entries), []);
        }
        catch (JsonException exception)
        {
            return new(null, [$"版本清单不是有效 JSON：{exception.Message}"]);
        }
    }

    private static bool IsSafeVersionId(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id == Path.GetFileName(id) &&
        !id.Contains(Path.DirectorySeparatorChar) &&
        !id.Contains(Path.AltDirectorySeparatorChar);

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static string? GetNestedString(JsonElement element, string objectName, string propertyName) =>
        element.TryGetProperty(objectName, out var nested) && nested.ValueKind == JsonValueKind.Object ? GetString(nested, propertyName) : null;

    private static Uri? GetHttpUri(JsonElement element, string propertyName)
    {
        var text = GetString(element, propertyName);
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri : null;
    }

    private static DateTimeOffset? GetDateTime(JsonElement element, string propertyName) =>
        DateTimeOffset.TryParse(GetString(element, propertyName), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result) ? result : null;
}
