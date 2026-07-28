using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Infrastructure;

public sealed class JsonCommunityFavoritesStore(IPlatformPaths platformPaths) : ICommunityFavoritesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SemaphoreSlim saveLock = new(1, 1);

    public async Task<CommunityFavoritesLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = GetPath();
        if (!File.Exists(path))
        {
            return new([CommunityFavoriteFolder.Create("默认")], null);
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var folders = JsonSerializer.Deserialize<List<CommunityFavoriteFolder>>(json, SerializerOptions);
            if (folders is null || folders.Count == 0 || folders.Any(folder => folder is null || !folder.IsValid) ||
                folders.Select(folder => folder.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != folders.Count)
            {
                return InvalidResult();
            }

            return new(folders, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return InvalidResult();
        }
    }

    public async Task SaveAsync(
        IReadOnlyList<CommunityFavoriteFolder> folders,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folders);
        if (folders.Count == 0 || folders.Any(folder => folder is null || !folder.IsValid) ||
            folders.Select(folder => folder.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != folders.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(folders), "收藏夹数据无效。");
        }

        await saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporaryPath = string.Empty;
        try
        {
            var path = GetPath();
            var directory = Path.GetDirectoryName(path) ?? throw new IOException("无法确定收藏夹目录。");
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(directory, $".community-favorites.{Guid.NewGuid():N}.json.partial");
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(folders, SerializerOptions),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryPath) && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            saveLock.Release();
        }
    }

    private string GetPath() => Path.Combine(
        Path.GetFullPath(platformPaths.Get().ApplicationDataDirectory),
        "community-favorites.json");

    private static CommunityFavoritesLoadResult InvalidResult() =>
        new([CommunityFavoriteFolder.Create("默认")], "本地收藏夹数据无效，已使用新的默认收藏夹。");
}
