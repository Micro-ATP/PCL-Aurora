using System.Security.Cryptography;
using System.Text;

namespace PCL.Aurora.Domain;

/// <summary>
/// Forge/NeoForge URL 组合直接参照 PCL-CE 的 ForgelikeInjector，OptiFine 公开目录与
/// 安装器主类参照 PCL-CE 的 ModDownload/ModDownloadLib；移除 Windows 路径与 Java Wrapper。
/// </summary>
public static class MinecraftLoaderInstallerPlanBuilder
{
    public static MinecraftLoaderInstallerPlan Build(
        MinecraftLoaderCatalogEntry loader,
        string minecraftRootDirectory,
        JavaInstallation? java,
        Uri? fabricInstallerUri = null,
        MinecraftLegacyOptiFineInstallation? legacyOptiFineInstallation = null)
    {
        ArgumentNullException.ThrowIfNull(loader);
        var errors = new List<string>();
        if (!IsSafeToken(loader.MinecraftVersion, 64) || !IsSafeLoaderVersion(loader.Version, 128))
        {
            errors.Add("加载器版本包含不安全字符。 ");
        }

        if (string.IsNullOrWhiteSpace(minecraftRootDirectory) || !Path.IsPathFullyQualified(minecraftRootDirectory))
        {
            errors.Add("Minecraft 根目录必须是绝对路径。 ");
        }

        var isLegacyOptiFine = IsLegacyOptiFine(loader);
        if (isLegacyOptiFine && legacyOptiFineInstallation is null)
        {
            errors.Add("未找到可安全继承的旧版 Minecraft 元数据。 ");
        }

        if (!isLegacyOptiFine &&
            (java is null || !java.IsCompatible || string.IsNullOrWhiteSpace(java.ExecutablePath) || !Path.IsPathFullyQualified(java.ExecutablePath)))
        {
            errors.Add("请先选择兼容的 Java。 ");
        }

        var artifact = errors.Count == 0 ? CreateArtifact(loader, fabricInstallerUri, legacyOptiFineInstallation, errors) : null;
        if (artifact is null)
        {
            return new(loader, null, null, errors);
        }

        var root = Path.GetFullPath(minecraftRootDirectory);
        var installerPath = Path.Combine(root, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (isLegacyOptiFine)
        {
            return new(loader, artifact, null, [], legacyOptiFineInstallation);
        }

        IReadOnlyList<string> arguments = loader.Kind switch
        {
            MinecraftLoaderKind.Forge or MinecraftLoaderKind.NeoForge =>
            ["-jar", installerPath, "--installClient", root],
            MinecraftLoaderKind.Fabric =>
            ["-jar", installerPath, "client", "-dir", root, "-mcversion", loader.MinecraftVersion, "-loader", loader.Version, "-noprofile"],
            MinecraftLoaderKind.OptiFine =>
            ["-cp", installerPath, "optifine.Installer"],
            _ => throw new ArgumentOutOfRangeException(nameof(loader)),
        };

        return new(
            loader,
            artifact,
            new(java!.ExecutablePath, root, arguments),
            []);
    }

    private static MinecraftDownloadArtifact? CreateArtifact(
        MinecraftLoaderCatalogEntry loader,
        Uri? fabricInstallerUri,
        MinecraftLegacyOptiFineInstallation? legacyOptiFineInstallation,
        ICollection<string> errors)
    {
        var officialSource = loader.Kind switch
        {
            MinecraftLoaderKind.Forge => $"https://maven.minecraftforge.net/net/minecraftforge/forge/{Uri.EscapeDataString(loader.MinecraftVersion)}-{Uri.EscapeDataString(loader.Version)}/forge-{Uri.EscapeDataString(loader.MinecraftVersion)}-{Uri.EscapeDataString(loader.Version)}-installer.jar",
            MinecraftLoaderKind.NeoForge => CreateNeoForgeUrl(loader, errors),
            MinecraftLoaderKind.Fabric when fabricInstallerUri is not null => fabricInstallerUri.AbsoluteUri,
            MinecraftLoaderKind.Fabric => null,
            MinecraftLoaderKind.OptiFine => CreateOptiFineUrl(loader, errors),
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(officialSource) || !Uri.TryCreate(officialSource, UriKind.Absolute, out var officialUri) || officialUri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add(loader.Kind switch
            {
                MinecraftLoaderKind.Fabric => "未找到可验证的 Fabric 官方稳定安装器。",
                MinecraftLoaderKind.OptiFine => "无法构造 OptiFine 公开下载地址。",
                _ => "无法构造加载器官方安装器地址。",
            });
            return null;
        }

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{loader.Kind}:{loader.MinecraftVersion}:{loader.Version}"))).ToLowerInvariant();
        var mirrorUri = CreateBmclApiMirrorUri(officialUri);
        return new(
            $"{loader.Kind} {loader.Version} 安装器",
            legacyOptiFineInstallation?.LibraryRelativePath ?? $".pcl-aurora/installers/{loader.Kind.ToString().ToLowerInvariant()}-{key}.jar",
            mirrorUri ?? officialUri,
            Sha1: null,
            Size: null,
            AlternativeUrls: mirrorUri is null ? [] : [officialUri],
            Sha1Url: loader.Kind == MinecraftLoaderKind.OptiFine ? null : new Uri(officialUri.AbsoluteUri + ".sha1"),
            MinimumSize: loader.Kind == MinecraftLoaderKind.OptiFine ? 300 * 1024 : null);
    }

    private static string? CreateOptiFineUrl(MinecraftLoaderCatalogEntry loader, ICollection<string> errors)
    {
        if (loader.OptiFineEntry is not { } optiFine ||
            !IsSafeFileName(optiFine.FileName) ||
            !IsSafeToken(optiFine.Type, 32) ||
            !IsSafeToken(optiFine.Patch, 96))
        {
            errors.Add("OptiFine 目录条目缺少可验证的公开下载字段。 ");
            return null;
        }

        var minecraftVersion = loader.MinecraftVersion is "1.8" or "1.9"
            ? loader.MinecraftVersion + ".0"
            : loader.MinecraftVersion;
        return $"https://bmclapi2.bangbang93.com/optifine/{Uri.EscapeDataString(minecraftVersion)}/{optiFine.DownloadPath}";
    }

    private static string? CreateNeoForgeUrl(MinecraftLoaderCatalogEntry loader, ICollection<string> errors)
    {
        if (loader.ForgelikeEntry is not PclCeNeoForgeListEntry neoForge ||
            !string.Equals(neoForge.Inherit, loader.MinecraftVersion, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("NeoForge 目录条目缺少可验证的官方 Maven 归属。 ");
            return null;
        }

        return neoForge.UrlBase + "-installer.jar";
    }

    // Directly adapted from PCL-CE's ModDownloadLib.ForgelikeInjector mirror mapping.
    // The Aurora executor adds a mandatory official SHA-1 sidecar check before this mirror is executable.
    private static Uri? CreateBmclApiMirrorUri(Uri officialUri)
    {
        if (officialUri.Scheme != Uri.UriSchemeHttps ||
            officialUri.Host is not ("maven.minecraftforge.net" or "files.minecraftforge.net" or "maven.neoforged.net" or "maven.fabricmc.net"))
        {
            return null;
        }

        var path = officialUri.Host == "maven.neoforged.net" && officialUri.AbsolutePath.StartsWith("/releases/", StringComparison.Ordinal)
            ? officialUri.AbsolutePath["/releases".Length..]
            : officialUri.AbsolutePath;
        return new Uri($"https://bmclapi2.bangbang93.com/maven{path}");
    }

    private static bool IsSafeToken(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximumLength &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+');

    private static bool IsSafeLoaderVersion(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximumLength &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+' or ' ');

    private static bool IsSafeFileName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 160 &&
        value.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_');

    public static bool IsLegacyOptiFine(MinecraftLoaderCatalogEntry loader) =>
        loader.Kind == MinecraftLoaderKind.OptiFine && !IsModernOptiFineMinecraftVersion(loader.MinecraftVersion);

    private static bool IsModernOptiFineMinecraftVersion(string value)
    {
        var segments = value.Split('.');
        return segments.Length >= 2 &&
               string.Equals(segments[0], "1", StringComparison.Ordinal) &&
               int.TryParse(segments[1], out var minorVersion) &&
               minorVersion >= 14;
    }
}
