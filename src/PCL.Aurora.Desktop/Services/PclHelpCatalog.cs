using System.IO.Compression;
using System.Text.Json;
using Avalonia.Platform;
using PCL.Aurora.Desktop.Models;

namespace PCL.Aurora.Desktop.Services;

// HelpEntry schema and bundled-catalog discovery adapt PCL2 ModMain.vb.
internal static class PclHelpCatalog
{
    private static readonly Uri CatalogUri =
        new("avares://PCL.Aurora.Desktop/Assets/Help/Pcl2Help.zip");

    public static IReadOnlyList<PclHelpEntry> Load()
    {
        using var source = AssetLoader.Open(CatalogUri);
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: false);
        var files = archive.Entries.ToDictionary(
            entry => NormalizePath(entry.FullName),
            StringComparer.OrdinalIgnoreCase);
        var result = new List<PclHelpEntry>();

        foreach (var pair in files.Where(pair => pair.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            using var jsonStream = pair.Value.Open();
            using var document = JsonDocument.Parse(jsonStream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            var root = document.RootElement;
            var title = GetString(root, "Title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var isEvent = GetBoolean(root, "IsEvent", false);
            string? content = null;
            if (!isEvent)
            {
                var xamlPath = pair.Key[..^5] + ".xaml";
                if (!files.TryGetValue(xamlPath, out var xamlEntry))
                {
                    continue;
                }

                using var reader = new StreamReader(xamlEntry.Open());
                content = reader.ReadToEnd();
            }

            result.Add(new PclHelpEntry(
                pair.Key,
                title.Trim(),
                GetString(root, "Description")?.Trim() ?? string.Empty,
                GetString(root, "Keywords")?.Trim() ?? string.Empty,
                GetStrings(root, "Types"),
                GetString(root, "Logo"),
                GetBoolean(root, "ShowInSearch", true),
                GetBoolean(root, "ShowInPublic", true),
                GetBoolean(root, "ShowInSnapshot", true),
                isEvent,
                GetString(root, "EventType"),
                GetString(root, "EventData"),
                content));
        }

        return result;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool GetBoolean(JsonElement root, string propertyName, bool fallback) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    private static IReadOnlyList<string> GetStrings(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            .Select(item => item.GetString()!.Trim())
            .ToArray();
    }
}
