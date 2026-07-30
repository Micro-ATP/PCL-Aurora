using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Aurora.Application;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Infrastructure;

/// <summary>
/// 将非敏感启动器偏好保存为应用数据目录中的 JSON 文件。
/// </summary>
public sealed class JsonLauncherPreferencesStore(IPlatformPaths platformPaths) : ILauncherPreferencesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SemaphoreSlim saveLock = new(1, 1);

    public async Task<LauncherPreferencesLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var preferencesPath = GetPreferencesPath();
        if (!File.Exists(preferencesPath))
        {
            return new LauncherPreferencesLoadResult(LauncherPreferences.Default, null);
        }

        try
        {
            var json = await File.ReadAllTextAsync(preferencesPath, cancellationToken).ConfigureAwait(false);
            var preferences = JsonSerializer.Deserialize<LauncherPreferences>(json, SerializerOptions);
            if (preferences is null || !preferences.IsValid)
            {
                return InvalidResult();
            }

            if (preferences.GameManagementOptions is null && preferences.DownloadConcurrency == 4)
            {
                preferences = preferences with
                {
                    DownloadConcurrency = LauncherDownloadSettings.DefaultConcurrency,
                };
            }

            return new LauncherPreferencesLoadResult(preferences, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return InvalidResult();
        }
    }

    public async Task SaveAsync(LauncherPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (!preferences.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(preferences), "启动器偏好包含不支持的值。");
        }

        await saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporaryPath = string.Empty;
        try
        {
            var preferencesPath = GetPreferencesPath();
            var directory = Path.GetDirectoryName(preferencesPath)
                ?? throw new InvalidOperationException("无法确定启动器偏好目录。");
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(directory, $".preferences.{Guid.NewGuid():N}.json.partial");
            var json = JsonSerializer.Serialize(preferences, SerializerOptions);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, preferencesPath, overwrite: true);
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

    private string GetPreferencesPath() => Path.Combine(
        Path.GetFullPath(platformPaths.Get().ApplicationDataDirectory),
        "preferences.json");

    private static LauncherPreferencesLoadResult InvalidResult() =>
        new(LauncherPreferences.Default, "本地启动器偏好无效，已安全回退为默认值。");
}
