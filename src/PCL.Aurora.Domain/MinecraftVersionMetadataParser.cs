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
                    assetIndex,
                    ParseLaunchMetadata(root),
                    ParseLibraries(root)),
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

    private static MinecraftLaunchMetadata? ParseLaunchMetadata(JsonElement root)
    {
        var hasArguments = TryGetObject(root, "arguments", out var arguments);
        var jvmArguments = hasArguments ? ParseLaunchArguments(arguments, "jvm") : ParsedLaunchArguments.Empty;
        var gameArguments = hasArguments ? ParseLaunchArguments(arguments, "game") : ParsedLaunchArguments.Empty;
        var legacyGameArguments = GetString(root, "minecraftArguments");
        var mainClass = GetString(root, "mainClass");
        return hasArguments || !string.IsNullOrWhiteSpace(legacyGameArguments) || !string.IsNullOrWhiteSpace(mainClass)
            ? new MinecraftLaunchMetadata(
                mainClass,
                jvmArguments.UnconditionalValues,
                gameArguments.UnconditionalValues,
                hasArguments,
                jvmArguments.ConditionalValues.Count > 0 || gameArguments.ConditionalValues.Count > 0,
                legacyGameArguments,
                jvmArguments.ConditionalValues,
                gameArguments.ConditionalValues,
                jvmArguments.HasUnsupportedValues || gameArguments.HasUnsupportedValues)
            : null;
    }

    private static IReadOnlyList<MinecraftVersionLibrary> ParseLibraries(JsonElement root)
    {
        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<MinecraftVersionLibrary>();
        foreach (var library in libraries.EnumerateArray())
        {
            if (library.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetString(library, "name") ?? "未命名库";
            var hasConditionalRules = library.TryGetProperty("rules", out var rulesElement);
            IReadOnlyList<MinecraftLaunchRule>? rules = null;
            var hasUnsupportedRules = hasConditionalRules && !TryParseLaunchRules(rulesElement, out rules);
            var artifactPath = default(string);
            MinecraftVersionDownload? artifact = null;
            var nativeClassifiers = ParseNativeClassifiers(library);
            var classifiers = ParseClassifiers(library);
            if (TryGetObject(library, "downloads", out var downloads) && TryGetObject(downloads, "artifact", out var artifactInfo))
            {
                artifactPath = GetString(artifactInfo, "path");
                if (artifactPath is not null && TryParseHttpUri(GetString(artifactInfo, "url"), out var artifactUrl))
                {
                    artifact = new MinecraftVersionDownload(
                        artifactUrl!,
                        GetString(artifactInfo, "sha1"),
                        GetInt64(artifactInfo, "size"));
                }
            }

            result.Add(new MinecraftVersionLibrary(
                name,
                artifactPath,
                artifact,
                hasConditionalRules,
                nativeClassifiers,
                classifiers,
                rules,
                hasUnsupportedRules));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string>? ParseNativeClassifiers(JsonElement library)
    {
        if (!TryGetObject(library, "natives", out var natives))
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var native in natives.EnumerateObject())
        {
            if (native.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(native.Value.GetString()))
            {
                result[native.Name] = native.Value.GetString()!;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyDictionary<string, MinecraftVersionLibraryClassifier>? ParseClassifiers(JsonElement library)
    {
        if (!TryGetObject(library, "downloads", out var downloads) ||
            !TryGetObject(downloads, "classifiers", out var classifierDownloads))
        {
            return null;
        }

        var result = new Dictionary<string, MinecraftVersionLibraryClassifier>(StringComparer.Ordinal);
        foreach (var classifier in classifierDownloads.EnumerateObject())
        {
            if (classifier.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            MinecraftVersionDownload? download = null;
            if (TryParseHttpUri(GetString(classifier.Value, "url"), out var downloadUri))
            {
                download = new MinecraftVersionDownload(
                    downloadUri!,
                    GetString(classifier.Value, "sha1"),
                    GetInt64(classifier.Value, "size"));
            }

            result[classifier.Name] = new MinecraftVersionLibraryClassifier(
                GetString(classifier.Value, "path"),
                download);
        }

        return result.Count == 0 ? null : result;
    }

    private static ParsedLaunchArguments ParseLaunchArguments(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return ParsedLaunchArguments.Empty;
        }

        var unconditionalValues = new List<string>();
        var conditionalValues = new List<MinecraftConditionalLaunchArgument>();
        var hasUnsupportedValues = false;
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                unconditionalValues.Add(value.GetString()!);
            }
            else if (value.ValueKind == JsonValueKind.Object && TryParseConditionalLaunchArgument(value, out var conditional))
            {
                conditionalValues.Add(conditional!);
            }
            else
            {
                hasUnsupportedValues = true;
            }
        }

        return new(unconditionalValues, conditionalValues, hasUnsupportedValues);
    }

    private static bool TryParseConditionalLaunchArgument(
        JsonElement element,
        out MinecraftConditionalLaunchArgument? conditional)
    {
        conditional = null;
        if (!element.TryGetProperty("value", out var value) || !TryParseArgumentValues(value, out var values))
        {
            return false;
        }

        IReadOnlyList<MinecraftLaunchRule>? rules = null;
        if (element.TryGetProperty("rules", out var rulesElement) && !TryParseLaunchRules(rulesElement, out rules))
        {
            return false;
        }

        conditional = new(values, rules);
        return true;
    }

    private static bool TryParseArgumentValues(JsonElement element, out IReadOnlyList<string> values)
    {
        values = [];
        if (element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString()))
        {
            values = [element.GetString()!];
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var result = new List<string>();
        foreach (var value in element.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            {
                return false;
            }

            result.Add(value.GetString()!);
        }

        values = result;
        return result.Count > 0;
    }

    private static bool TryParseLaunchRules(JsonElement element, out IReadOnlyList<MinecraftLaunchRule>? rules)
    {
        rules = null;
        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var result = new List<MinecraftLaunchRule>();
        foreach (var rule in element.EnumerateArray())
        {
            if (rule.ValueKind != JsonValueKind.Object || !TryParseLaunchRule(rule, out var parsedRule))
            {
                return false;
            }

            result.Add(parsedRule!);
        }

        rules = result;
        return true;
    }

    private static bool TryParseLaunchRule(JsonElement element, out MinecraftLaunchRule? rule)
    {
        rule = null;
        var action = string.Equals(GetString(element, "action"), "allow", StringComparison.OrdinalIgnoreCase)
            ? MinecraftLaunchRuleAction.Allow
            : MinecraftLaunchRuleAction.Disallow;
        MinecraftLaunchRuleOperatingSystem? operatingSystem = null;
        if (element.TryGetProperty("os", out var os))
        {
            if (os.ValueKind != JsonValueKind.Object ||
                !TryParseOptionalString(os, "name", out var name) ||
                !TryParseOptionalString(os, "version", out var version) ||
                !TryParseOptionalString(os, "arch", out var architecture))
            {
                return false;
            }

            operatingSystem = new(name, version, architecture);
        }

        IReadOnlyDictionary<string, bool>? features = null;
        if (element.TryGetProperty("features", out var featureElement))
        {
            if (featureElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var feature in featureElement.EnumerateObject())
            {
                if (feature.Value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                {
                    return false;
                }

                result[feature.Name] = feature.Value.GetBoolean();
            }

            features = result;
        }

        rule = new(action, operatingSystem, features);
        return true;
    }

    private static bool TryParseOptionalString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private sealed record ParsedLaunchArguments(
        IReadOnlyList<string> UnconditionalValues,
        IReadOnlyList<MinecraftConditionalLaunchArgument> ConditionalValues,
        bool HasUnsupportedValues)
    {
        public static ParsedLaunchArguments Empty { get; } = new([], [], false);
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

    private static bool TryParseHttpUri(string? value, out Uri? uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate) &&
            (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps))
        {
            uri = candidate;
            return true;
        }

        uri = null;
        return false;
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
