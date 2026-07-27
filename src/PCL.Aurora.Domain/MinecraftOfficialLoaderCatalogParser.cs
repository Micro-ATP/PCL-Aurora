using System.Text.Json;
using System.Xml.Linq;

namespace PCL.Aurora.Domain;

/// <summary>
/// 解析 Forge Maven、NeoForge Maven API 与 Fabric Meta 的官方目录响应。
/// 该解析器不发起网络请求，也不产生下载或安装行为。
/// </summary>
public static class MinecraftOfficialLoaderCatalogParser
{
    public static MinecraftLoaderCatalogParseResult Parse(
        string minecraftVersion,
        string? forgeMetadataXml,
        string? neoForgeReleasesJson,
        string? neoForgeLegacyJson,
        string? fabricLoaderJson,
        string? optiFineVersionListJson = null)
    {
        if (!IsSafeToken(minecraftVersion, 64))
        {
            return new(null, ["Minecraft 版本号无效。"]);
        }

        try
        {
            var entries = new List<MinecraftLoaderCatalogEntry>();
            if (!string.IsNullOrWhiteSpace(forgeMetadataXml)) AddForgeEntries(entries, minecraftVersion, forgeMetadataXml);
            if (!string.IsNullOrWhiteSpace(neoForgeReleasesJson)) AddNeoForgeEntries(entries, minecraftVersion, neoForgeReleasesJson);
            if (!string.IsNullOrWhiteSpace(neoForgeLegacyJson)) AddNeoForgeEntries(entries, minecraftVersion, neoForgeLegacyJson);
            if (!string.IsNullOrWhiteSpace(fabricLoaderJson)) AddFabricEntries(entries, minecraftVersion, fabricLoaderJson);
            if (!string.IsNullOrWhiteSpace(optiFineVersionListJson)) AddOptiFineEntries(entries, minecraftVersion, optiFineVersionListJson);

            var ordered = entries
                .GroupBy(entry => $"{entry.Kind}:{entry.MinecraftVersion}:{entry.Version}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(entry => entry.Kind)
                .ThenBy(entry => entry.Channel)
                .ThenByDescending(entry => entry.Version, new PclCeVersionComparer.VersionComparer())
                .ToArray();
            return ordered.Length == 0
                ? new(null, [$"官方目录中没有兼容 Minecraft {minecraftVersion} 的加载器版本。"])
                : new(new("Forge / NeoForge / Fabric 官方目录与 OptiFine 公开目录", ordered), []);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or ArgumentException)
        {
            return new(null, [$"官方加载器目录格式无效：{exception.Message}"]);
        }
    }

    private static void AddForgeEntries(List<MinecraftLoaderCatalogEntry> entries, string minecraftVersion, string metadataXml)
    {
        var document = XDocument.Parse(metadataXml, LoadOptions.None);
        var prefix = minecraftVersion + "-";
        foreach (var value in document.Descendants().Where(element => element.Name.LocalName == "version").Select(element => element.Value.Trim()))
        {
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var version = value[prefix.Length..];
            if (!IsSafeToken(version, 128) || version.Contains('-', StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                entries.Add(new(
                    MinecraftLoaderKind.Forge,
                    minecraftVersion,
                    version,
                    MinecraftLoaderChannel.Release,
                    IsRecommended: false,
                    new PclCeForgeVersionEntry(version, branch: null, minecraftVersion)));
            }
            catch (ArgumentException)
            {
                // Maven metadata may retain legacy aliases that are not Forge version values.
            }
        }
    }

    private static void AddNeoForgeEntries(List<MinecraftLoaderCatalogEntry> entries, string minecraftVersion, string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("versions", out var versions) ||
            versions.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("NeoForge Maven 响应缺少 versions 数组。 ");
        }

        foreach (var item in versions.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var version = item.GetString()?.Trim();
            if (!IsSafeToken(version, 128) || !PclCeNeoForgeVersionPattern.IsMatch(version!))
            {
                continue;
            }

            var entry = new PclCeNeoForgeListEntry(version!);
            if (!string.Equals(entry.Inherit, minecraftVersion, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entries.Add(new(
                MinecraftLoaderKind.NeoForge,
                minecraftVersion,
                version!,
                entry.IsBeta ? MinecraftLoaderChannel.Beta : MinecraftLoaderChannel.Release,
                IsRecommended: false,
                entry));
        }
    }

    private static void AddFabricEntries(List<MinecraftLoaderCatalogEntry> entries, string minecraftVersion, string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Fabric Meta 响应不是数组。 ");
        }

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("loader", out var loader) || loader.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var version = loader.TryGetProperty("version", out var versionElement) && versionElement.ValueKind == JsonValueKind.String
                ? versionElement.GetString()?.Trim()
                : null;
            if (!IsSafeToken(version, 128))
            {
                continue;
            }

            var stable = loader.TryGetProperty("stable", out var stableElement) && stableElement.ValueKind == JsonValueKind.True;
            entries.Add(new(
                MinecraftLoaderKind.Fabric,
                minecraftVersion,
                version!,
                stable ? MinecraftLoaderChannel.Release : MinecraftLoaderChannel.Beta,
                IsRecommended: stable,
                ForgelikeEntry: null));
        }
    }

    private static void AddOptiFineEntries(List<MinecraftLoaderCatalogEntry> entries, string minecraftVersion, string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("OptiFine 公开目录响应不是数组。 ");
        }

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !TryGetString(item, "mcversion", out var inheritedVersion) ||
                !TryGetString(item, "type", out var type) ||
                !TryGetString(item, "patch", out var patch) ||
                !TryGetString(item, "filename", out var fileName) ||
                !string.Equals(inheritedVersion, minecraftVersion, StringComparison.OrdinalIgnoreCase) ||
                !IsSafeToken(type, 32) ||
                !IsSafeToken(patch, 96) ||
                !IsSafeFileName(fileName))
            {
                continue;
            }

            var isPreview = patch.Contains("pre", StringComparison.OrdinalIgnoreCase);
            var requiredForgeVersion = TryGetString(item, "forge", out var forge) &&
                                       !forge.Contains("N/A", StringComparison.OrdinalIgnoreCase)
                ? forge.Replace("Forge ", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("#", string.Empty, StringComparison.Ordinal)
                : null;
            var displayType = type.StartsWith("HD_U_", StringComparison.OrdinalIgnoreCase)
                ? type["HD_U_".Length..]
                : type.Replace("HD_U", string.Empty, StringComparison.OrdinalIgnoreCase).Trim('_', ' ');
            var displayVersion = $"{displayType} {patch}".Trim();
            entries.Add(new(
                MinecraftLoaderKind.OptiFine,
                minecraftVersion,
                displayVersion,
                isPreview ? MinecraftLoaderChannel.Beta : MinecraftLoaderChannel.Release,
                IsRecommended: !isPreview,
                ForgelikeEntry: null,
                new(fileName, type, patch, isPreview, requiredForgeVersion)));
        }
    }

    private static bool IsSafeToken(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximumLength &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+' or ' ');

    private static bool IsSafeFileName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 160 &&
        value.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_');

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim() ?? string.Empty
            : string.Empty;
        return value.Length > 0;
    }
}
