using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Aurora.Application;

/// <summary>
/// Directly adapts PCL-CE's Minecraft 1.16.5 Authlib replacement while using
/// structured JSON updates instead of global text replacement.
/// </summary>
public static class PclCeAuthlibMetadataFixer
{
    private const string OldCoordinate = "com.mojang:authlib:2.1.28";
    private const string NewCoordinate = "com.mojang:authlib:2.3.31";
    private const string OldPathSegment = "2.1.28/authlib-2.1.28.jar";
    private const string NewPathSegment = "2.3.31/authlib-2.3.31.jar";
    private const string NewSha1 = "bbd00ca33b052f73a6312254780fc580d2da3535";
    private const long NewSize = 87662;

    public static string Apply(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidDataException("版本元数据根节点必须是对象。");
        if (root["libraries"] is not JsonArray libraries)
        {
            return json;
        }

        var changed = false;
        foreach (var library in libraries.OfType<JsonObject>())
        {
            if (!string.Equals(library["name"]?.GetValue<string>(), OldCoordinate, StringComparison.Ordinal))
            {
                continue;
            }

            library["name"] = NewCoordinate;
            if (library["downloads"]?["artifact"] is JsonObject artifact)
            {
                ReplacePath(artifact, "path");
                ReplacePath(artifact, "url");
                artifact["sha1"] = NewSha1;
                artifact["size"] = NewSize;
            }
            changed = true;
        }

        return changed
            ? root.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            : json;
    }

    private static void ReplacePath(JsonObject artifact, string propertyName)
    {
        if (artifact[propertyName]?.GetValue<string>() is { } value)
        {
            artifact[propertyName] = value.Replace(OldPathSegment, NewPathSegment, StringComparison.Ordinal);
        }
    }
}
