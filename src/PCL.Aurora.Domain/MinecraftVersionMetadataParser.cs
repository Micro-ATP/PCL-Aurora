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
        var hasConditionalJvmArguments = false;
        var hasConditionalGameArguments = false;
        var jvmArguments = hasArguments ? ParseStringArray(arguments, "jvm", out hasConditionalJvmArguments) : [];
        var gameArguments = hasArguments ? ParseStringArray(arguments, "game", out hasConditionalGameArguments) : [];
        var legacyGameArguments = GetString(root, "minecraftArguments");
        var mainClass = GetString(root, "mainClass");
        return hasArguments || !string.IsNullOrWhiteSpace(legacyGameArguments) || !string.IsNullOrWhiteSpace(mainClass)
            ? new MinecraftLaunchMetadata(
                mainClass,
                jvmArguments,
                gameArguments,
                hasArguments,
                hasConditionalJvmArguments || hasConditionalGameArguments,
                legacyGameArguments)
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
            var hasConditionalRules = library.TryGetProperty("rules", out _);
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
                classifiers));
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

    private static IReadOnlyList<string> ParseStringArray(
        JsonElement arguments,
        string propertyName,
        out bool hasConditionalArguments)
    {
        hasConditionalArguments = false;
        if (!arguments.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                result.Add(value.GetString()!);
            }
            else if (value.ValueKind != JsonValueKind.String)
            {
                hasConditionalArguments = true;
            }
        }

        return result;
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
