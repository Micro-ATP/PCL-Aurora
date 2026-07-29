// Directly adapts PCL-CE's CurseForge file mapping from
// Plain Craft Launcher 2/Modules/Minecraft/ModComp.cs for Aurora's domain model.
using System.Globalization;
using System.Text.Json;

namespace PCL.Aurora.Domain;

public static class CurseForgeCommunityResourceVersionParser
{
    public static CommunityResourceVersionCatalog ParseCatalog(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CommunityResourceVersionCatalog.Failure("CurseForge 返回了空的世界版本目录。");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return CommunityResourceVersionCatalog.Failure("CurseForge 世界版本目录缺少文件数组。");
            }

            return ParseElements(data.EnumerateArray());
        }
        catch (JsonException exception)
        {
            return CommunityResourceVersionCatalog.Failure($"CurseForge 世界版本目录不是有效 JSON：{exception.Message}");
        }
    }

    public static CommunityResourceVersionCatalog ParseSingle(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CommunityResourceVersionCatalog.Failure("CurseForge 返回了空的世界文件信息。");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("data", out var data))
            {
                return CommunityResourceVersionCatalog.Failure("CurseForge 世界文件信息缺少 data。");
            }

            return ParseElements([data]);
        }
        catch (JsonException exception)
        {
            return CommunityResourceVersionCatalog.Failure($"CurseForge 世界文件信息不是有效 JSON：{exception.Message}");
        }
    }

    private static CommunityResourceVersionCatalog ParseElements(IEnumerable<JsonElement> elements)
    {
        var versions = new List<CommunityResourceVersion>();
        var errors = new List<string>();
        foreach (var element in elements)
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

    private static bool TryParseVersion(
        JsonElement element,
        out CommunityResourceVersion? version,
        out string? error)
    {
        version = null;
        error = null;
        var id = GetInt64(element, "id");
        var projectId = GetInt64(element, "modId");
        var fileName = GetString(element, "fileName");
        var name = GetString(element, "displayName") ?? fileName;
        var size = GetInt64(element, "fileLength");
        var sha1 = GetSha1(element);
        if (id <= 0 || projectId <= 0 || !IsSafeFileName(fileName) ||
            string.IsNullOrWhiteSpace(name) || size <= 0 || !IsSha1(sha1))
        {
            error = "CurseForge 世界文件缺少安全文件名、大小或 SHA-1。";
            return false;
        }

        var url = GetDownloadUri(element, id, fileName!);
        if (url is null)
        {
            error = $"{name} 没有安全的 HTTPS 下载地址。";
            return false;
        }

        var rawVersions = GetStringArray(element, "gameVersions");
        var loaders = rawVersions.Where(IsLoader).Select(NormalizeLoader).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var gameVersions = rawVersions.Where(IsMinecraftVersion).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var idText = id.ToString(CultureInfo.InvariantCulture);
        version = new(
            idText,
            projectId.ToString(CultureInfo.InvariantCulture),
            name.Trim(),
            name.Trim(),
            GetInt32(element, "releaseType") switch
            {
                1 => CommunityResourceVersionChannel.Release,
                2 => CommunityResourceVersionChannel.Beta,
                _ => CommunityResourceVersionChannel.Alpha,
            },
            ParseDate(GetString(element, "fileDate")),
            Math.Max(0, GetInt64(element, "downloadCount")),
            gameVersions,
            loaders,
            [new(fileName!, url, sha1!, size, true)],
            []);
        return true;
    }

    private static Uri? GetDownloadUri(JsonElement element, long id, string fileName)
    {
        var address = GetString(element, "downloadUrl");
        if (TryCreateForgeCdnUri(address, out var uri))
        {
            return uri;
        }

        var idText = id.ToString(CultureInfo.InvariantCulture);
        if (idText.Length < 5)
        {
            return null;
        }

        return new Uri($"https://edge.forgecdn.net/files/{idText[..4]}/{idText[4..]}/{Uri.EscapeDataString(fileName)}");
    }

    private static bool TryCreateForgeCdnUri(string? text, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var candidate) ||
            candidate.Scheme != Uri.UriSchemeHttps ||
            !(candidate.Host.Equals("forgecdn.net", StringComparison.OrdinalIgnoreCase) ||
              candidate.Host.EndsWith(".forgecdn.net", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private static string? GetSha1(JsonElement element)
    {
        if (!element.TryGetProperty("hashes", out var hashes) || hashes.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var hash in hashes.EnumerateArray())
        {
            if (GetInt32(hash, "algo") == 1)
            {
                return GetString(hash, "value");
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray()
            : [];

    private static bool IsLoader(string value) => value.ToLowerInvariant() is
        "forge" or "neoforge" or "fabric" or "quilt" or "iris" or "optifine";

    private static string NormalizeLoader(string value) => value.ToLowerInvariant();

    private static bool IsMinecraftVersion(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 && parts.All(part => part.All(char.IsAsciiDigit));
    }

    private static bool IsSafeFileName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 240 && value is not "." and not ".." &&
        value.IndexOfAny(['/', '\\']) < 0 && value.All(character => !char.IsControl(character));

    private static bool IsSha1(string? value) => value is { Length: 40 } && value.All(Uri.IsHexDigit);

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

    private static long GetInt64(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var result)
            ? result
            : 0;

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var result) ? result : null;
}
