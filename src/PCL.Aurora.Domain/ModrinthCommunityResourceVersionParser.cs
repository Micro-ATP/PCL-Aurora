// Directly adapts Modrinth version parsing rules from PCL2
// Plain Craft Launcher 2/Modules/Resource/ResourceVersion.vb and PCL-CE
// Plain Craft Launcher 2/Modules/Minecraft/ModComp.cs into Aurora's domain model.
using System.Text.Json;

namespace PCL.Aurora.Domain;

public static class ModrinthCommunityResourceVersionParser
{
    public static CommunityResourceVersionCatalog ParseCatalog(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CommunityResourceVersionCatalog.Failure("Modrinth 返回了空的版本目录。");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return CommunityResourceVersionCatalog.Failure("Modrinth 版本目录不是数组。");
            }

            var versions = new List<CommunityResourceVersion>();
            var errors = new List<string>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (TryParseVersion(element, out var version, out var error))
                {
                    versions.Add(version!);
                }
                else
                {
                    errors.Add(error!);
                }
            }

            return new(versions, errors);
        }
        catch (JsonException exception)
        {
            return CommunityResourceVersionCatalog.Failure($"Modrinth 版本目录不是有效 JSON：{exception.Message}");
        }
    }

    public static CommunityResourceVersionCatalog ParseSingle(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CommunityResourceVersionCatalog.Failure("Modrinth 返回了空的版本信息。");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return TryParseVersion(document.RootElement, out var version, out var error)
                ? new([version!], [])
                : CommunityResourceVersionCatalog.Failure(error!);
        }
        catch (JsonException exception)
        {
            return CommunityResourceVersionCatalog.Failure($"Modrinth 版本信息不是有效 JSON：{exception.Message}");
        }
    }

    private static bool TryParseVersion(
        JsonElement element,
        out CommunityResourceVersion? version,
        out string? error)
    {
        version = null;
        error = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "Modrinth 版本目录包含非对象条目。";
            return false;
        }

        var id = GetString(element, "id");
        var projectId = GetString(element, "project_id");
        var name = GetString(element, "name");
        var versionNumber = GetString(element, "version_number");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(projectId) ||
            string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(versionNumber))
        {
            error = "Modrinth 版本条目缺少 ID、项目 ID、名称或版本号。";
            return false;
        }

        var files = ParseFiles(element).ToArray();
        if (files.Length == 0)
        {
            error = $"{name} 没有可校验的 HTTPS 下载文件。";
            return false;
        }

        version = new(
            id,
            projectId,
            name.Trim(),
            versionNumber.Trim(),
            ParseChannel(GetString(element, "version_type")),
            ParseDate(GetString(element, "date_published")),
            GetInt64(element, "downloads"),
            GetStringArray(element, "game_versions"),
            GetStringArray(element, "loaders"),
            files,
            ParseDependencies(element));
        return true;
    }

    private static IEnumerable<CommunityResourceVersionFile> ParseFiles(JsonElement element)
    {
        if (!element.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var file in files.EnumerateArray())
        {
            var fileName = GetString(file, "filename");
            var urlText = GetString(file, "url");
            var size = GetInt64(file, "size");
            var sha1 = file.TryGetProperty("hashes", out var hashes) ? GetString(hashes, "sha1") : null;
            if (!IsSafeFileName(fileName) ||
                !Uri.TryCreate(urlText, UriKind.Absolute, out var url) ||
                url.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(url.Host, "cdn.modrinth.com", StringComparison.OrdinalIgnoreCase) ||
                size <= 0 ||
                !IsSha1(sha1))
            {
                continue;
            }

            yield return new(
                fileName!,
                url,
                sha1!,
                size,
                file.TryGetProperty("primary", out var primary) && primary.ValueKind == JsonValueKind.True);
        }
    }

    private static IReadOnlyList<CommunityResourceDependency> ParseDependencies(JsonElement element)
    {
        if (!element.TryGetProperty("dependencies", out var dependencies) ||
            dependencies.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return dependencies.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new CommunityResourceDependency(
                GetString(item, "project_id"),
                GetString(item, "version_id"),
                GetString(item, "file_name"),
                ParseDependencyType(GetString(item, "dependency_type"))))
            .Where(item => !string.IsNullOrWhiteSpace(item.ProjectId) || !string.IsNullOrWhiteSpace(item.VersionId))
            .ToArray();
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static CommunityResourceVersionChannel ParseChannel(string? value) => value?.ToLowerInvariant() switch
    {
        "release" => CommunityResourceVersionChannel.Release,
        "beta" => CommunityResourceVersionChannel.Beta,
        _ => CommunityResourceVersionChannel.Alpha,
    };

    private static CommunityResourceDependencyType ParseDependencyType(string? value) => value?.ToLowerInvariant() switch
    {
        "required" => CommunityResourceDependencyType.Required,
        "optional" => CommunityResourceDependencyType.Optional,
        "incompatible" => CommunityResourceDependencyType.Incompatible,
        _ => CommunityResourceDependencyType.Embedded,
    };

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long GetInt64(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.TryGetInt64(out var result)
            ? result
            : 0;

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var result) ? result : null;

    private static bool IsSha1(string? value) =>
        value is { Length: 40 } && value.All(Uri.IsHexDigit);

    private static bool IsSafeFileName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 180 &&
        value is not "." and not ".." &&
        value.IndexOfAny(['/', '\\']) < 0 &&
        value.All(character => !char.IsControl(character));
}
