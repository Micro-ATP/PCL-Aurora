// Directly adapts PCL-CE's CurseForge world catalog field mapping from
// Plain Craft Launcher 2/Modules/Minecraft/ModComp.cs for Aurora's domain model.
using System.Text.Json;

namespace PCL.Aurora.Domain;

public static class CurseForgeCommunityResourceParser
{
    public static CommunityResourceSearchResult Parse(
        string json,
        CommunityResourceType requestedType = CommunityResourceType.World)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CommunityResourceSearchResult.Failure("CurseForge 返回了空的资源目录。");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return CommunityResourceSearchResult.Failure("CurseForge 资源目录缺少项目数组。");
            }

            var projects = new List<CommunityResourceProject>();
            var errors = new List<string>();
            foreach (var item in data.EnumerateArray())
            {
                if (TryParseProject(item, requestedType, out var project, out var error))
                {
                    projects.Add(project!);
                }
                else
                {
                    errors.Add(error!);
                }
            }

            var pagination = root.TryGetProperty("pagination", out var value) ? value : default;
            var offset = GetInt32(pagination, "index");
            var limit = GetInt32(pagination, "pageSize");
            var total = GetInt32(pagination, "totalCount");
            return new(projects, Math.Max(0, offset), Math.Max(0, limit), Math.Max(0, total), errors);
        }
        catch (JsonException exception)
        {
            return CommunityResourceSearchResult.Failure($"CurseForge 资源目录不是有效 JSON：{exception.Message}");
        }
    }

    private static bool TryParseProject(
        JsonElement item,
        CommunityResourceType requestedType,
        out CommunityResourceProject? project,
        out string? error)
    {
        project = null;
        error = null;
        var id = GetInt64(item, "id") ?? 0;
        if (item.ValueKind != JsonValueKind.Object ||
            id <= 0 ||
            string.IsNullOrWhiteSpace(GetString(item, "slug")) ||
            string.IsNullOrWhiteSpace(GetString(item, "name")))
        {
            error = "CurseForge 项目缺少 ID、短名或名称。";
            return false;
        }

        var slug = GetString(item, "slug")!;
        var title = GetString(item, "name")!;
        var websiteText = item.TryGetProperty("links", out var links) ? GetString(links, "websiteUrl") : null;
        if (!TryCreateHttpsUri(websiteText, "curseforge.com", out var website))
        {
            website = new Uri($"https://www.curseforge.com/minecraft/{GetWebsiteCategory(requestedType)}/{Uri.EscapeDataString(slug)}");
        }

        Uri? icon = null;
        if (item.TryGetProperty("logo", out var logo))
        {
            var iconText = GetString(logo, "thumbnailUrl") ?? GetString(logo, "url");
            if (TryCreateHttpsUri(iconText, "forgecdn.net", out var parsedIcon))
            {
                icon = parsedIcon;
            }
        }

        var categories = GetObjectArray(item, "categories")
            .Select(category => GetString(category, "slug"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fileIndexes = GetObjectArray(item, "latestFilesIndexes").ToArray();
        var gameVersions = fileIndexes
            .Select(index => GetString(index, "gameVersion"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var author = GetObjectArray(item, "authors")
            .Select(value => GetString(value, "name"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "未知作者";

        project = new(
            id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            slug.Trim(),
            title.Trim(),
            GetString(item, "summary")?.Trim() ?? string.Empty,
            author,
            requestedType,
            website!,
            icon,
            Math.Max(0, GetInt64(item, "downloadCount") ?? 0),
            0,
            ParseDate(GetString(item, "dateModified")),
            fileIndexes.Select(index => GetString(index, "filename"))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            categories,
            gameVersions)
        {
            Loaders = [],
        };
        return true;
    }

    private static IEnumerable<JsonElement> GetObjectArray(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.Object)
            : [];

    private static bool TryCreateHttpsUri(string? text, string hostSuffix, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var candidate) ||
            candidate.Scheme != Uri.UriSchemeHttps ||
            !(candidate.Host.Equals(hostSuffix, StringComparison.OrdinalIgnoreCase) ||
              candidate.Host.EndsWith('.' + hostSuffix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetInt32(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
            ? result
            : 0;

    private static long? GetInt64(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var result)
            ? result
            : null;

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var result) ? result : null;

    private static string GetWebsiteCategory(CommunityResourceType type) => type switch
    {
        CommunityResourceType.Mod => "mc-mods",
        CommunityResourceType.ModPack => "modpacks",
        CommunityResourceType.DataPack => "data-packs",
        CommunityResourceType.ResourcePack => "texture-packs",
        CommunityResourceType.Shader => "shaders",
        CommunityResourceType.World => "worlds",
        _ => "worlds",
    };
}
