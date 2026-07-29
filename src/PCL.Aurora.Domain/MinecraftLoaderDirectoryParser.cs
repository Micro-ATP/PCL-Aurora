using System.Text.Json;

namespace PCL.Aurora.Domain;

/// <summary>
/// Converts the public catalogs used by PCL-CE into platform-neutral package groups.
/// It performs no network or file-system operations.
/// </summary>
public static class MinecraftLoaderDirectoryParser
{
    private static readonly PclCeVersionComparer.VersionComparer VersionComparer = new();

    public static MinecraftLoaderDirectory ParseForgeMinecraftVersions(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Forge Minecraft 版本目录不是数组。");
        }

        var groups = document.RootElement.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim() ?? string.Empty : string.Empty)
            .Where(version => IsSafeToken(version, 64))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(version => version, VersionComparer)
            .Select(version => new MinecraftLoaderDirectoryGroup(version, version, [], IsLazy: true))
            .ToArray();
        return CreateDirectory(MinecraftLoaderKind.Forge, "BMCLAPI Forge 目录", groups);
    }

    public static MinecraftLoaderDirectory ParseForgeVersions(string minecraftVersion, string json)
    {
        if (!IsSafeToken(minecraftVersion, 64))
        {
            throw new FormatException("Minecraft 版本号无效。");
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Forge 版本目录不是数组。");
        }

        var entries = new List<MinecraftLoaderPackageEntry>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !TryGetString(item, "version", out var version) ||
                !IsSafeToken(version, 64))
            {
                continue;
            }

            var branch = TryGetString(item, "branch", out var parsedBranch) && IsSafeToken(parsedBranch, 64)
                ? parsedBranch
                : null;
            var selectedFile = SelectForgeFile(item);
            if (selectedFile is null)
            {
                continue;
            }

            var forge = new PclCeForgeVersionEntry(version, branch, minecraftVersion)
            {
                Category = selectedFile.Value.Category,
                Hash = selectedFile.Value.Hash ?? string.Empty,
                IsRecommended = TryGetBoolean(item, "recommended"),
                ReleaseTime = TryGetString(item, "modified", out var modified) ? modified : string.Empty,
            };
            var extension = selectedFile.Value.Format;
            var coordinate = $"{minecraftVersion}-{forge.FileVersion}";
            var artifactName = $"forge-{coordinate}-{forge.Category}.{extension}";
            var official = new Uri($"https://maven.minecraftforge.net/net/minecraftforge/forge/{coordinate}/{artifactName}");
            var mirror = new Uri($"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{coordinate}/{artifactName}");
            var localReleaseTime = DateTimeOffset.TryParse(
                forge.ReleaseTime,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var releaseTime)
                ? releaseTime.ToLocalTime().ToString("yyyy/M/d HH:mm", System.Globalization.CultureInfo.CurrentCulture)
                : forge.ReleaseTime;
            var information = string.IsNullOrWhiteSpace(localReleaseTime)
                ? string.Empty
                : $"发布时间：{localReleaseTime}";
            entries.Add(new(
                MinecraftLoaderKind.Forge,
                minecraftVersion,
                version,
                version,
                MinecraftLoaderChannel.Release,
                forge.IsRecommended,
                $"Forge-{minecraftVersion}-{version}.{extension}",
                mirror,
                [official],
                new Uri($"https://maven.minecraftforge.net/net/minecraftforge/forge/{coordinate}/forge-{coordinate}-changelog.txt"),
                information));
        }

        var ordered = entries.OrderByDescending(entry => entry.Version, VersionComparer).ToArray();
        return CreateDirectory(
            MinecraftLoaderKind.Forge,
            "BMCLAPI Forge 目录",
            [new(minecraftVersion, minecraftVersion, ordered)]);
    }

    public static MinecraftLoaderDirectory ParseNeoForgeVersions(params string?[] responses)
    {
        var entries = new List<MinecraftLoaderPackageEntry>();
        foreach (var response in responses.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            using var document = JsonDocument.Parse(response!);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("NeoForge Maven 响应不是对象。");
            }

            foreach (var apiName in EnumerateNeoForgeVersionNames(document.RootElement))
            {
                if (!IsSafeToken(apiName, 128))
                {
                    continue;
                }

                PclCeNeoForgeListEntry neoForge;
                try
                {
                    neoForge = new(apiName);
                }
                catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
                {
                    continue;
                }

                var url = new Uri(neoForge.UrlBase + "-installer.jar");
                var mirror = new Uri(url.AbsoluteUri.Replace(
                    "maven.neoforged.net/releases",
                    "bmclapi2.bangbang93.com/maven",
                    StringComparison.Ordinal));
                entries.Add(new(
                    MinecraftLoaderKind.NeoForge,
                    neoForge.Inherit,
                    apiName,
                    neoForge.VersionName,
                    neoForge.IsBeta ? MinecraftLoaderChannel.Beta : MinecraftLoaderChannel.Release,
                    IsRecommended: false,
                    $"NeoForge-{neoForge.Inherit}-{neoForge.VersionName}.jar",
                    mirror,
                    [url],
                    new Uri(neoForge.UrlBase + "-changelog.txt"),
                    neoForge.IsBeta ? "测试版" : "稳定版"));
            }
        }

        var groups = entries
            .GroupBy(entry => entry.MinecraftVersion, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Key, VersionComparer)
            .Select(group => new MinecraftLoaderDirectoryGroup(
                group.Key,
                $"{group.Key} ({group.Count()})",
                group.OrderByDescending(entry => entry.Version, VersionComparer).ToArray()))
            .ToArray();
        return CreateDirectory(MinecraftLoaderKind.NeoForge, "NeoForge Maven", groups);
    }

    public static MinecraftLoaderDirectory ParseOptiFineVersions(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("OptiFine 公开目录不是数组。");
        }

        var entries = new List<MinecraftLoaderPackageEntry>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !TryGetString(item, "mcversion", out var minecraftVersion) ||
                !TryGetString(item, "type", out var type) ||
                !TryGetString(item, "patch", out var patch) ||
                !TryGetString(item, "filename", out var fileName) ||
                !IsSafeToken(minecraftVersion, 64) || !IsSafeToken(type, 64) ||
                !IsSafeToken(patch, 96) || !IsSafeFileName(fileName))
            {
                continue;
            }

            var isPreview = patch.Contains("pre", StringComparison.OrdinalIgnoreCase);
            var requiredForgeVersion = TryGetString(item, "forge", out var forge) &&
                                       !forge.Contains("N/A", StringComparison.OrdinalIgnoreCase)
                ? forge.Replace("Forge ", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("#", string.Empty, StringComparison.Ordinal)
                : null;
            var optiFine = new PclCeOptiFineVersionEntry(fileName, type, patch, isPreview, requiredForgeVersion);
            var displayType = type.StartsWith("HD_U_", StringComparison.OrdinalIgnoreCase)
                ? type["HD_U_".Length..]
                : type.Replace("HD_U", string.Empty, StringComparison.OrdinalIgnoreCase).Trim('_', ' ');
            var displayVersion = $"{displayType} {patch}".Trim();
            var downloadMinecraftVersion = minecraftVersion is "1.8" or "1.9" ? minecraftVersion + ".0" : minecraftVersion;
            var information = isPreview ? "预览版" : "正式版";
            information += requiredForgeVersion is null
                ? "  |  不兼容 Forge"
                : $"  |  兼容 Forge {requiredForgeVersion}";
            entries.Add(new(
                MinecraftLoaderKind.OptiFine,
                minecraftVersion,
                displayVersion,
                $"{minecraftVersion} {displayVersion}",
                isPreview ? MinecraftLoaderChannel.Beta : MinecraftLoaderChannel.Release,
                IsRecommended: !isPreview,
                fileName,
                new Uri($"https://bmclapi2.bangbang93.com/optifine/{downloadMinecraftVersion}/{optiFine.DownloadPath}"),
                [],
                new Uri($"https://optifine.net/changelog?f={Uri.EscapeDataString(fileName)}"),
                information));
        }

        var groups = entries
            .GroupBy(entry => GetOptiFineGroup(entry.MinecraftVersion), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key == "Snapshot" ? 0 : 1)
            .ThenByDescending(group => group.Key, VersionComparer)
            .Select(group => new MinecraftLoaderDirectoryGroup(
                group.Key,
                $"{(group.Key == "Snapshot" ? "快照版" : group.Key)} ({group.Count()})",
                group.OrderByDescending(entry => entry.DisplayName, VersionComparer).ToArray()))
            .ToArray();
        return CreateDirectory(MinecraftLoaderKind.OptiFine, "BMCLAPI OptiFine 目录", groups);
    }

    public static MinecraftLoaderDirectory ParseFabricInstallers(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Fabric 安装器目录不是数组。");
        }

        var entries = new List<MinecraftLoaderPackageEntry>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !TryGetString(item, "version", out var version) ||
                !IsSafeToken(version, 96) || !TryGetString(item, "url", out var urlText) ||
                !Uri.TryCreate(urlText, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(url.Host, "maven.fabricmc.net", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = Path.GetFileName(url.AbsolutePath);
            if (!IsSafeFileName(fileName))
            {
                continue;
            }

            var stable = TryGetBoolean(item, "stable");
            entries.Add(new(
                MinecraftLoaderKind.Fabric,
                string.Empty,
                version,
                version.Replace("+build", string.Empty, StringComparison.OrdinalIgnoreCase),
                stable ? MinecraftLoaderChannel.Release : MinecraftLoaderChannel.Beta,
                IsRecommended: stable,
                fileName,
                url,
                [],
                new Uri("https://fabricmc.net/blog"),
                stable ? "稳定版" : "测试版"));
        }

        var ordered = entries.OrderByDescending(entry => entry.Version, VersionComparer).ToArray();
        return CreateDirectory(
            MinecraftLoaderKind.Fabric,
            "Fabric Meta",
            [new("installers", $"版本列表 ({ordered.Length})", ordered, IsCollapsible: false)]);
    }

    public static MinecraftLoaderDirectory ParseCleanroomVersions(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Cleanroom GitHub Releases 响应不是数组。");
        }

        var entries = new List<MinecraftLoaderPackageEntry>();
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object || !TryGetString(release, "tag_name", out var tag) ||
                !IsSafeToken(tag, 96) || !release.TryGetProperty("assets", out var assets) ||
                assets.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var expectedAssetName = $"cleanroom-{tag}-installer.jar";
            var installer = assets.EnumerateArray().FirstOrDefault(asset =>
                asset.ValueKind == JsonValueKind.Object &&
                TryGetString(asset, "name", out var name) &&
                string.Equals(name, expectedAssetName, StringComparison.OrdinalIgnoreCase));
            if (installer.ValueKind != JsonValueKind.Object ||
                !TryGetString(installer, "browser_download_url", out var downloadText) ||
                !TryCreateHttpsUri(downloadText, "github.com", out var downloadUri))
            {
                continue;
            }

            var isPreview = tag.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
                            tag.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
                            tag.Contains("pre", StringComparison.OrdinalIgnoreCase) ||
                            tag.Contains("rc", StringComparison.OrdinalIgnoreCase);
            var changelogUri = TryGetString(release, "html_url", out var htmlText) &&
                               TryCreateHttpsUri(htmlText, "github.com", out var parsedChangelog)
                ? parsedChangelog
                : new Uri($"https://github.com/CleanroomMC/Cleanroom/releases/tag/{Uri.EscapeDataString(tag)}");
            entries.Add(new(
                MinecraftLoaderKind.Cleanroom,
                "1.12.2",
                tag,
                tag,
                isPreview ? MinecraftLoaderChannel.Beta : MinecraftLoaderChannel.Release,
                IsRecommended: !isPreview,
                $"Cleanroom-1.12.2-{tag}.jar",
                downloadUri,
                [],
                changelogUri,
                isPreview ? "测试版" : "稳定版"));
        }

        var ordered = entries.OrderByDescending(entry => entry.Version, VersionComparer).ToArray();
        return CreateDirectory(
            MinecraftLoaderKind.Cleanroom,
            "Cleanroom GitHub Releases",
            [new("1.12.2", $"1.12.2 ({ordered.Length})", ordered)]);
    }

    public static MinecraftLoaderDirectory ParseLegacyFabricInstallers(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("installer", out var installers) ||
            installers.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Legacy Fabric 安装器目录格式无效。");
        }

        var entries = new List<MinecraftLoaderPackageEntry>();
        foreach (var item in installers.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !TryGetString(item, "version", out var version) ||
                !IsSafeToken(version, 96) || !TryGetString(item, "url", out var urlText) ||
                !TryCreateHttpsUri(urlText, "maven.legacyfabric.net", out var url))
            {
                continue;
            }

            var fileName = Path.GetFileName(url.AbsolutePath);
            if (!IsSafeFileName(fileName) || !fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var stable = TryGetBoolean(item, "stable");
            entries.Add(new(
                MinecraftLoaderKind.LegacyFabric,
                string.Empty,
                version,
                version,
                stable ? MinecraftLoaderChannel.Release : MinecraftLoaderChannel.Beta,
                IsRecommended: stable,
                fileName,
                url,
                [],
                ChangelogUri: null,
                stable ? "稳定版" : "测试版"));
        }

        var ordered = entries.OrderByDescending(entry => entry.Version, VersionComparer).ToArray();
        return CreateDirectory(
            MinecraftLoaderKind.LegacyFabric,
            "Legacy Fabric Meta",
            [new("installers", $"版本列表 ({ordered.Length})", ordered, IsCollapsible: false)]);
    }

    public static MinecraftLoaderDirectory ParseLabyModVersions(string productionJson, string snapshotJson)
    {
        var entries = new[]
        {
            ParseLabyModChannel(productionJson, "production", "正式版", "LabyMod4ProductionInstaller.jar", isRecommended: true),
            ParseLabyModChannel(snapshotJson, "snapshot", "快照版", "LabyMod4SnapshotInstaller.jar", isRecommended: false),
        };
        return CreateDirectory(
            MinecraftLoaderKind.LabyMod,
            "LabyMod Releases",
            [new("channels", $"版本列表 ({entries.Length})", entries, IsCollapsible: false)]);
    }

    public static MinecraftLoaderDirectory ParseLiteLoaderVersions(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("versions", out var versions) ||
            versions.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("LiteLoader 版本目录格式无效。");
        }

        var entries = new List<MinecraftLoaderPackageEntry>();
        foreach (var versionProperty in versions.EnumerateObject())
        {
            var minecraftVersion = versionProperty.Name.Trim();
            if (!IsSafeToken(minecraftVersion, 64) || minecraftVersion.StartsWith("1.5", StringComparison.Ordinal) ||
                minecraftVersion.StartsWith("1.6", StringComparison.Ordinal) ||
                versionProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var descriptorRoot = versionProperty.Value.TryGetProperty("artefacts", out var artifacts)
                ? artifacts
                : versionProperty.Value.TryGetProperty("snapshots", out var snapshots)
                    ? snapshots
                    : default;
            if (descriptorRoot.ValueKind != JsonValueKind.Object ||
                !descriptorRoot.TryGetProperty("com.mumfrey:liteloader", out var coordinate) ||
                coordinate.ValueKind != JsonValueKind.Object ||
                !coordinate.TryGetProperty("latest", out var latest) || latest.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var isPreview = TryGetString(latest, "stream", out var stream) &&
                            string.Equals(stream, "snapshot", StringComparison.OrdinalIgnoreCase);
            var loaderVersion = TryGetString(latest, "version", out var parsedVersion) && IsSafeToken(parsedVersion, 96)
                ? parsedVersion
                : minecraftVersion;
            var generatedFileName = $"liteloader-installer-{minecraftVersion}{(minecraftVersion is "1.8" or "1.9" ? ".0" : string.Empty)}-00-SNAPSHOT.jar";
            var downloadUri = CreateLiteLoaderDownloadUri(minecraftVersion, generatedFileName);
            if (downloadUri is null)
            {
                continue;
            }

            var information = isPreview ? "测试版" : "稳定版";
            if (TryGetInt64(latest, "timestamp", out var timestamp))
            {
                try
                {
                    var releaseTime = timestamp > 10_000_000_000
                        ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                        : DateTimeOffset.FromUnixTimeSeconds(timestamp);
                    information += $"  |  发布时间：{releaseTime.ToLocalTime():yyyy/M/d HH:mm}";
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Keep the stable/preview label when an upstream timestamp is malformed.
                }
            }

            entries.Add(new(
                MinecraftLoaderKind.LiteLoader,
                minecraftVersion,
                loaderVersion,
                minecraftVersion,
                isPreview ? MinecraftLoaderChannel.Beta : MinecraftLoaderChannel.Release,
                IsRecommended: !isPreview,
                generatedFileName.Replace("-SNAPSHOT", string.Empty, StringComparison.Ordinal),
                downloadUri,
                [],
                new Uri($"https://jenkins.liteloader.com/view/{Uri.EscapeDataString(minecraftVersion)}"),
                information,
                MinimumSize: 1_048_576));
        }

        var groups = entries
            .GroupBy(entry => GetMinecraftMainVersion(entry.MinecraftVersion), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Key, VersionComparer)
            .Select(group => new MinecraftLoaderDirectoryGroup(
                group.Key,
                $"{group.Key} ({group.Count()})",
                group.OrderByDescending(entry => entry.MinecraftVersion, VersionComparer).ToArray()))
            .ToArray();
        return CreateDirectory(MinecraftLoaderKind.LiteLoader, "LiteLoader 版本目录", groups);
    }

    private static MinecraftLoaderPackageEntry ParseLabyModChannel(
        string json,
        string channel,
        string channelLabel,
        string fileName,
        bool isRecommended)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !TryGetString(document.RootElement, "labyModVersion", out var version) ||
            !IsSafeToken(version, 96))
        {
            throw new FormatException($"LabyMod {channel} 目录格式无效。");
        }

        var information = channelLabel;
        if (TryGetString(document.RootElement, "releaseTime", out var releaseTimeText) &&
            DateTimeOffset.TryParse(releaseTimeText, out var releaseTime))
        {
            information += $"  |  发布时间：{releaseTime.ToLocalTime():yyyy/M/d HH:mm}";
        }

        return new(
            MinecraftLoaderKind.LabyMod,
            string.Empty,
            version,
            $"{version} {channelLabel}",
            channel == "production" ? MinecraftLoaderChannel.Release : MinecraftLoaderChannel.Beta,
            isRecommended,
            fileName,
            new Uri($"https://releases.labymod.net/api/v1/installer/{channel}/java"),
            [],
            new Uri("https://www.labymod.net/zh_Hans/download"),
            information);
    }

    private static Uri? CreateLiteLoaderDownloadUri(string minecraftVersion, string generatedFileName) => minecraftVersion switch
    {
        "1.7.10" => new Uri("https://dl.liteloader.com/redist/1.7.10/liteloader-installer-1.7.10-04.jar"),
        "1.7.2" => new Uri("https://dl.liteloader.com/redist/1.7.2/liteloader-installer-1.7.2-04.jar"),
        _ when minecraftVersion.StartsWith("1.", StringComparison.Ordinal) => new Uri(
            $"https://jenkins.liteloader.com/job/LiteLoaderInstaller%20{Uri.EscapeDataString(minecraftVersion)}/lastSuccessfulBuild/artifact/{(minecraftVersion == "1.8" ? "ant/dist/" : "build/libs/")}{generatedFileName}"),
        _ => null,
    };

    private static string GetMinecraftMainVersion(string minecraftVersion)
    {
        var parts = minecraftVersion.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : minecraftVersion;
    }

    private static MinecraftLoaderDirectory CreateDirectory(
        MinecraftLoaderKind kind,
        string sourceName,
        IReadOnlyList<MinecraftLoaderDirectoryGroup> groups)
    {
        if (groups.Count == 0)
        {
            throw new FormatException($"{kind} 目录中没有可用版本。");
        }

        return new(kind, sourceName, groups);
    }

    private static (string Category, string Format, string? Hash)? SelectForgeFile(JsonElement item)
    {
        if (!item.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        (string Category, string Format, string? Hash)? best = null;
        var priority = -1;
        foreach (var file in files.EnumerateArray())
        {
            if (file.ValueKind != JsonValueKind.Object || !TryGetString(file, "category", out var category) ||
                !TryGetString(file, "format", out var format))
            {
                continue;
            }

            var candidatePriority = (category, format) switch
            {
                ("installer", "jar") => 3,
                ("universal", "zip") => 2,
                ("client", "zip") => 1,
                _ => 0,
            };
            if (candidatePriority <= priority)
            {
                continue;
            }

            priority = candidatePriority;
            best = (category, format, TryGetString(file, "hash", out var hash) ? hash : null);
        }

        return priority > 0 ? best : null;
    }

    private static string GetOptiFineGroup(string minecraftVersion)
    {
        if (!minecraftVersion.StartsWith("1.", StringComparison.Ordinal))
        {
            return "Snapshot";
        }

        var parts = minecraftVersion.Split('.');
        return parts.Length >= 2 ? $"1.{parts[1]}" : "Snapshot";
    }

    private static bool IsSafeToken(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+' or ' ');

    private static bool IsSafeFileName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 180 &&
        value is not "." and not ".." && string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal) &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+');

    private static bool TryGetString(JsonElement item, string propertyName, out string value)
    {
        value = item.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim() ?? string.Empty
            : string.Empty;
        return value.Length > 0;
    }

    private static bool TryGetBoolean(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;

    private static bool TryGetInt64(JsonElement item, string propertyName, out long value)
    {
        value = 0;
        if (!item.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind == JsonValueKind.Number
            ? property.TryGetInt64(out value)
            : property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out value);
    }

    private static bool TryCreateHttpsUri(string value, string expectedHost, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps &&
            string.Equals(parsed.Host, expectedHost, StringComparison.OrdinalIgnoreCase))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    private static IEnumerable<string> EnumerateNeoForgeVersionNames(JsonElement root)
    {
        if (root.TryGetProperty("versions", out var versions) && versions.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in versions.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString()?.Trim() is { Length: > 0 } version)
                {
                    yield return version;
                }
            }
        }

        if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in files.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && TryGetString(item, "name", out var version))
                {
                    yield return version;
                }
            }
        }
    }
}
