using System.Security.Cryptography;
using System.Text;

namespace PCL.Aurora.Domain;

/// <summary>
/// Forge/NeoForge URL 字段组合直接参照 PCL-CE 的 ForgelikeInjector，保留官方 Maven
/// 源和 installer 命名规则；已移除 Windows 路径、镜像回退及 Java Wrapper。
/// </summary>
public static class MinecraftLoaderInstallerPlanBuilder
{
    public static MinecraftLoaderInstallerPlan Build(
        MinecraftLoaderCatalogEntry loader,
        string minecraftRootDirectory,
        JavaInstallation? java,
        Uri? fabricInstallerUri = null)
    {
        ArgumentNullException.ThrowIfNull(loader);
        var errors = new List<string>();
        if (!IsSafeToken(loader.MinecraftVersion, 64) || !IsSafeToken(loader.Version, 128))
        {
            errors.Add("加载器版本包含不安全字符。 ");
        }

        if (string.IsNullOrWhiteSpace(minecraftRootDirectory) || !Path.IsPathFullyQualified(minecraftRootDirectory))
        {
            errors.Add("Minecraft 根目录必须是绝对路径。 ");
        }

        if (java is null || !java.IsCompatible || string.IsNullOrWhiteSpace(java.ExecutablePath) || !Path.IsPathFullyQualified(java.ExecutablePath))
        {
            errors.Add("请先选择兼容的 Java。 ");
        }

        var artifact = errors.Count == 0 ? CreateArtifact(loader, fabricInstallerUri, errors) : null;
        if (artifact is null)
        {
            return new(loader, null, null, errors);
        }

        var root = Path.GetFullPath(minecraftRootDirectory);
        var installerPath = Path.Combine(root, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        IReadOnlyList<string> arguments = loader.Kind switch
        {
            MinecraftLoaderKind.Forge or MinecraftLoaderKind.NeoForge =>
            ["-jar", installerPath, "--installClient", root],
            MinecraftLoaderKind.Fabric =>
            ["-jar", installerPath, "client", "-dir", root, "-mcversion", loader.MinecraftVersion, "-loader", loader.Version, "-noprofile"],
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
        ICollection<string> errors)
    {
        var officialSource = loader.Kind switch
        {
            MinecraftLoaderKind.Forge => $"https://maven.minecraftforge.net/net/minecraftforge/forge/{Uri.EscapeDataString(loader.MinecraftVersion)}-{Uri.EscapeDataString(loader.Version)}/forge-{Uri.EscapeDataString(loader.MinecraftVersion)}-{Uri.EscapeDataString(loader.Version)}-installer.jar",
            MinecraftLoaderKind.NeoForge => CreateNeoForgeUrl(loader, errors),
            MinecraftLoaderKind.Fabric when fabricInstallerUri is not null => fabricInstallerUri.AbsoluteUri,
            MinecraftLoaderKind.Fabric => null,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(officialSource) || !Uri.TryCreate(officialSource, UriKind.Absolute, out var officialUri) || officialUri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add(loader.Kind == MinecraftLoaderKind.Fabric
                ? "未找到可验证的 Fabric 官方稳定安装器。"
                : "无法构造加载器官方安装器地址。");
            return null;
        }

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{loader.Kind}:{loader.MinecraftVersion}:{loader.Version}"))).ToLowerInvariant();
        var mirrorUri = CreateBmclApiMirrorUri(officialUri);
        return new(
            $"{loader.Kind} {loader.Version} 安装器",
            $".pcl-aurora/installers/{loader.Kind.ToString().ToLowerInvariant()}-{key}.jar",
            mirrorUri ?? officialUri,
            Sha1: null,
            Size: null,
            AlternativeUrls: mirrorUri is null ? [] : [officialUri],
            Sha1Url: new Uri(officialUri.AbsoluteUri + ".sha1"));
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
}
