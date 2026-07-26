using System.Text.Json;

namespace PCL.Aurora.Domain;

/// <summary>
/// 解析 Fabric 官方 Meta 的安装器目录；仅接受 Fabric 官方 Maven 的 HTTPS JAR。
/// </summary>
public static class MinecraftFabricInstallerMetadataParser
{
    public static Uri? ParseLatestStableInstallerUri(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("stable", out var stable) || stable.ValueKind != JsonValueKind.True ||
                !item.TryGetProperty("version", out var versionElement) || versionElement.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("url", out var urlElement) || urlElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var version = versionElement.GetString()?.Trim();
            var url = urlElement.GetString()?.Trim();
            if (!IsSafeVersion(version) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsOfficialInstallerUri(uri, version!))
            {
                continue;
            }

            return uri;
        }

        return null;
    }

    private static bool IsOfficialInstallerUri(Uri uri, string version) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        string.Equals(uri.Host, "maven.fabricmc.net", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            uri.AbsolutePath,
            $"/net/fabricmc/fabric-installer/{Uri.EscapeDataString(version)}/fabric-installer-{Uri.EscapeDataString(version)}.jar",
            StringComparison.Ordinal);

    private static bool IsSafeVersion(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+');
}
