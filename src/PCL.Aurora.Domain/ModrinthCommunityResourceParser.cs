using System.Globalization;
using System.Text.Json;

namespace PCL.Aurora.Domain;

public static class ModrinthCommunityResourceParser
{
    public static CommunityResourceSearchResult Parse(string json, CommunityResourceType requestedType)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CommunityResourceSearchResult.Failure("Modrinth 返回了空的资源目录。");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
            {
                return CommunityResourceSearchResult.Failure("Modrinth 资源目录缺少 hits 数组。");
            }

            var errors = new List<string>();
            var projects = new List<CommunityResourceProject>();
            foreach (var hit in hits.EnumerateArray())
            {
                if (TryParseProject(hit, requestedType, out var project, out var error))
                {
                    projects.Add(project!);
                }
                else
                {
                    errors.Add(error!);
                }
            }

            return new(
                projects,
                GetInt32(root, "offset") ?? 0,
                GetInt32(root, "limit") ?? projects.Count,
                GetInt32(root, "total_hits") ?? projects.Count,
                errors);
        }
        catch (JsonException exception)
        {
            return CommunityResourceSearchResult.Failure($"Modrinth 资源目录不是有效 JSON：{exception.Message}");
        }
    }

    private static bool TryParseProject(
        JsonElement hit,
        CommunityResourceType requestedType,
        out CommunityResourceProject? project,
        out string? error)
    {
        project = null;
        error = null;
        if (hit.ValueKind != JsonValueKind.Object)
        {
            error = "Modrinth 资源目录包含非对象条目。";
            return false;
        }

        var id = GetString(hit, "project_id");
        var slug = GetString(hit, "slug");
        var title = GetString(hit, "title");
        var description = GetString(hit, "description") ?? string.Empty;
        var author = GetString(hit, "author") ?? "未知作者";
        var rawType = ParseType(GetString(hit, "project_type"));
        var rawCategories = GetStringArray(hit, "categories");
        var matchesRequestedType = rawType == requestedType ||
            requestedType == CommunityResourceType.DataPack &&
            rawType == CommunityResourceType.Mod &&
            rawCategories.Contains("datapack", StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(id) ||
            !IsSafeSlug(slug) ||
            string.IsNullOrWhiteSpace(title) ||
            rawType is null ||
            !matchesRequestedType)
        {
            error = $"Modrinth 资源目录包含无效或类型不匹配的项目：{title ?? id ?? "未知项目"}。";
            return false;
        }

        var websiteUrl = new Uri($"https://modrinth.com/{GetWebType(rawType.Value)}/{slug}");
        project = new(
            id,
            slug!,
            title.Trim(),
            description.Trim(),
            author.Trim(),
            requestedType,
            websiteUrl,
            GetHttpsUri(hit, "icon_url"),
            GetInt64(hit, "downloads") ?? 0,
            GetInt64(hit, "follows") ?? 0,
            GetDateTime(hit, "date_modified"),
            GetString(hit, "latest_version"),
            GetStringArray(hit, "display_categories", "categories"),
            GetStringArray(hit, "versions"))
        {
            Loaders = GetStringArray(hit, "categories")
                .Where(category => category.ToLowerInvariant() is
                    "forge" or "fabric" or "quilt" or "neoforge" or
                    "iris" or "optifine" or "vanilla")
                .ToArray(),
        };
        return true;
    }

    private static CommunityResourceType? ParseType(string? value) => value?.ToLowerInvariant() switch
    {
        "mod" => CommunityResourceType.Mod,
        "modpack" => CommunityResourceType.ModPack,
        "datapack" => CommunityResourceType.DataPack,
        "resourcepack" => CommunityResourceType.ResourcePack,
        "shader" => CommunityResourceType.Shader,
        _ => null,
    };

    private static string GetWebType(CommunityResourceType type) => type switch
    {
        CommunityResourceType.Mod => "mod",
        CommunityResourceType.ModPack => "modpack",
        CommunityResourceType.DataPack => "datapack",
        CommunityResourceType.ResourcePack => "resourcepack",
        CommunityResourceType.Shader => "shader",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    private static bool IsSafeSlug(string? slug) =>
        !string.IsNullOrWhiteSpace(slug) &&
        slug.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt32(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result) ? result : null;

    private static long? GetInt64(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var result) ? result : null;

    private static Uri? GetHttpsUri(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri : null;
    }

    private static DateTimeOffset? GetDateTime(JsonElement element, string propertyName) =>
        DateTimeOffset.TryParse(
            GetString(element, propertyName),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var value)
            ? value
            : null;

    private static IReadOnlyList<string> GetStringArray(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return values.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return [];
    }
}
