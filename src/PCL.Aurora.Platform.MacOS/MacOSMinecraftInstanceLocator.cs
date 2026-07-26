using System.Globalization;
using System.Text.Json;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Platform.MacOS;

public sealed class MacOSMinecraftInstanceLocator(string? minecraftDirectory = null) : IMinecraftInstanceLocator
{
    private readonly string rootDirectory = minecraftDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Application Support",
        "minecraft");

    public async Task<IReadOnlyList<MinecraftInstance>> FindAllAsync(CancellationToken cancellationToken = default)
    {
        var versionsDirectory = Path.Combine(rootDirectory, "versions");
        if (!Directory.Exists(versionsDirectory))
        {
            return [];
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(versionsDirectory).ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var instances = new List<MinecraftInstance>();
        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            instances.Add(await ReadInstanceAsync(directory, cancellationToken).ConfigureAwait(false));
        }

        return instances
            .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<MinecraftInstance> ReadInstanceAsync(string directory, CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(directory);
        var metadataPath = Path.Combine(directory, $"{name}.json");
        if (!File.Exists(metadataPath))
        {
            return CreateIncompleteInstance(name, directory);
        }

        try
        {
            await using var stream = File.OpenRead(metadataPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;

            var versionId = GetString(root, "id");
            var type = GetString(root, "type");
            var releaseTime = ParseReleaseTime(GetString(root, "releaseTime"));
            var inheritsFrom = GetString(root, "inheritsFrom");
            var installedLoader = MinecraftInstalledLoaderDetector.Detect(GetLibraryNames(root));
            var baseVersionId = inheritsFrom ?? installedLoader?.MinecraftVersion ?? versionId;
            return new MinecraftInstance(
                name,
                directory,
                versionId,
                type,
                releaseTime,
                MinecraftInstanceStatus.Valid,
                inheritsFrom,
                baseVersionId,
                installedLoader);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return CreateIncompleteInstance(name, directory);
        }
    }

    private static MinecraftInstance CreateIncompleteInstance(string name, string directory) =>
        new(name, directory, null, null, null, MinecraftInstanceStatus.Incomplete);

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static IEnumerable<string> GetLibraryNames(JsonElement root)
    {
        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var library in libraries.EnumerateArray())
        {
            var name = library.ValueKind == JsonValueKind.Object ? GetString(library, "name") : null;
            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return name;
            }
        }
    }

    private static DateTimeOffset? ParseReleaseTime(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;
}
