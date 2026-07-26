using System.Text.Json;

namespace PCL.Aurora.Domain;

public static class MinecraftAssetIndexParser
{
    public static MinecraftAssetIndexParseResult Parse(string id, string json)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return new(null, ["资源索引名称为空。"]);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return new(null, ["资源索引内容为空。"]);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("objects", out var objects) ||
                objects.ValueKind != JsonValueKind.Object)
            {
                return new(null, ["资源索引缺少 objects 对象。"]);
            }

            var errors = new List<string>();
            var parsedObjects = new List<MinecraftAssetObject>();
            foreach (var property in objects.EnumerateObject())
            {
                if (!IsSafeObjectName(property.Name))
                {
                    errors.Add($"资源对象名称无效：{property.Name}。");
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    errors.Add($"资源对象 {property.Name} 不是对象。");
                    continue;
                }

                var hash = GetString(property.Value, "hash");
                var size = GetInt64(property.Value, "size");
                if (!IsSha1(hash) || size is null)
                {
                    errors.Add($"资源对象 {property.Name} 缺少有效 SHA-1 或长度。");
                    continue;
                }

                parsedObjects.Add(new(property.Name, hash!, size.Value));
            }

            return errors.Count > 0
                ? new(null, errors)
                : new(new(
                    id,
                    parsedObjects,
                    GetBoolean(root, "virtual"),
                    GetBoolean(root, "map_to_resources")), []);
        }
        catch (JsonException exception)
        {
            return new(null, [$"资源索引不是有效 JSON：{exception.Message}"]);
        }
    }

    private static bool IsSafeObjectName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        !name.StartsWith("/", StringComparison.Ordinal) &&
        !name.Split('/').Any(segment => segment is "." or "..");

    private static bool IsSha1(string? value) =>
        value is { Length: 40 } && value.All(character => char.IsAsciiHexDigit(character));

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static long? GetInt64(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var value) && value >= 0
            ? value
            : null;

    private static bool GetBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;
}
