using System.Text.Json;

namespace PCL.Aurora.Domain;

public static class MinecraftLoaderCatalogParser
{
    public static MinecraftLoaderCatalogParseResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new(null, ["加载器目录为空。"]);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("loaders", out var loaders) ||
                loaders.ValueKind != JsonValueKind.Array)
            {
                return new(null, ["加载器目录缺少 loaders 数组。"]);
            }

            var sourceName = GetString(document.RootElement, "source") ?? "本地目录";
            if (!IsSafeToken(sourceName, 96))
            {
                return new(null, ["加载器目录来源名称无效。"]);
            }

            var entries = new List<MinecraftLoaderCatalogEntry>();
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var loader in loaders.EnumerateArray())
            {
                if (loader.ValueKind != JsonValueKind.Object)
                {
                    errors.Add("加载器目录包含非对象条目。");
                    continue;
                }

                var kindText = GetString(loader, "kind");
                var minecraftVersion = GetString(loader, "minecraftVersion");
                var version = GetString(loader, "version");
                if (!TryParseKind(kindText, out var kind) ||
                    !IsSafeToken(minecraftVersion, 64) ||
                    !IsSafeToken(version, 128))
                {
                    errors.Add($"加载器目录包含无效条目：{kindText ?? "未知类型"} {version ?? "未知版本"}。");
                    continue;
                }

                var forgelike = CreateForgelikeEntry(kind, minecraftVersion!, version!, out var constructionError);
                if (constructionError is not null)
                {
                    errors.Add(constructionError);
                    continue;
                }

                var channel = GetChannel(GetString(loader, "channel"), version!, forgelike);
                if (channel is null)
                {
                    errors.Add($"加载器版本 {version} 的发布通道无效。");
                    continue;
                }

                var key = $"{kind}:{minecraftVersion}:{version}";
                if (!seen.Add(key))
                {
                    errors.Add($"加载器目录包含重复版本：{kind} {minecraftVersion} {version}。");
                    continue;
                }

                entries.Add(new(kind, minecraftVersion!, version!, channel.Value, GetBoolean(loader, "recommended"), forgelike));
            }

            if (entries.Count == 0)
            {
                errors.Add("加载器目录不包含有效版本。 ");
            }

            return errors.Count == 0
                ? new(new(sourceName, entries), [])
                : new(null, errors);
        }
        catch (JsonException exception)
        {
            return new(null, [$"加载器目录不是有效 JSON：{exception.Message}"]);
        }
    }

    private static PclCeForgelikeEntry? CreateForgelikeEntry(
        MinecraftLoaderKind kind,
        string minecraftVersion,
        string version,
        out string? error)
    {
        try
        {
            error = null;
            return kind switch
            {
                MinecraftLoaderKind.Forge => new PclCeForgeVersionEntry(version, branch: null, minecraftVersion),
                MinecraftLoaderKind.NeoForge => ValidateNeoForge(minecraftVersion, version),
                MinecraftLoaderKind.Fabric => null,
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or IndexOutOfRangeException)
        {
            error = $"加载器版本 {version} 无法与 Minecraft {minecraftVersion} 兼容：{exception.Message}";
            return null;
        }
    }

    private static PclCeNeoForgeListEntry ValidateNeoForge(string minecraftVersion, string version)
    {
        if (!PclCeNeoForgeVersionPattern.IsMatch(version))
        {
            throw new FormatException("NeoForge 版本格式不受支持。");
        }

        var entry = new PclCeNeoForgeListEntry(version);
        if (!string.Equals(entry.Inherit, minecraftVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"NeoForge 版本归属 {entry.Inherit}，与目录声明的 {minecraftVersion} 不一致。");
        }

        return entry;
    }

    private static MinecraftLoaderChannel? GetChannel(string? value, string version, PclCeForgelikeEntry? forgelike)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return version.Contains("alpha", StringComparison.OrdinalIgnoreCase)
                    ? MinecraftLoaderChannel.Alpha
                    : forgelike is PclCeNeoForgeListEntry { IsBeta: true }
                      || version.Contains("beta", StringComparison.OrdinalIgnoreCase)
                        ? MinecraftLoaderChannel.Beta
                        : version.Contains("snapshot", StringComparison.OrdinalIgnoreCase)
                        ? MinecraftLoaderChannel.Snapshot
                        : MinecraftLoaderChannel.Release;
        }

        return value.ToLowerInvariant() switch
        {
            "release" => MinecraftLoaderChannel.Release,
            "beta" => MinecraftLoaderChannel.Beta,
            "alpha" => MinecraftLoaderChannel.Alpha,
            "snapshot" => MinecraftLoaderChannel.Snapshot,
            _ => null,
        };
    }

    private static bool TryParseKind(string? value, out MinecraftLoaderKind kind)
    {
        kind = value?.ToLowerInvariant() switch
        {
            "forge" => MinecraftLoaderKind.Forge,
            "neoforge" => MinecraftLoaderKind.NeoForge,
            "fabric" => MinecraftLoaderKind.Fabric,
            _ => default,
        };
        return value is not null && value.Equals(kind.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeToken(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximumLength &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+' or ' ');

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString()?.Trim() : null;

    private static bool GetBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True;
}
