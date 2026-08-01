namespace PCL.Aurora.Application;

/// <summary>
/// 统一实现 PCL-CE 的官方源/镜像源优先级。
/// 三档设置都会保留失败回退，但只改变首选顺序，避免一次不可用的镜像阻断下载。
/// </summary>
public static class PclCeDownloadSourceResolver
{
    public static IReadOnlyList<Uri> Order(
        DownloadSourcePreference preference,
        Uri official,
        Uri? mirror)
    {
        ArgumentNullException.ThrowIfNull(official);
        if (mirror is null || mirror == official)
        {
            return [official];
        }

        return preference switch
        {
            DownloadSourcePreference.Mirror => [mirror, official],
            DownloadSourcePreference.Official => [official, mirror],
            _ => [official, mirror],
        };
    }

    public static IReadOnlyList<Uri> OrderCommunity(
        DownloadSourcePreference preference,
        Uri official,
        Uri? mirror)
    {
        ArgumentNullException.ThrowIfNull(official);
        if (mirror is null || mirror == official)
        {
            return [official];
        }

        return preference switch
        {
            DownloadSourcePreference.Mirror => [mirror, official],
            DownloadSourcePreference.Official => [official],
            _ => [official, mirror],
        };
    }

    public static Uri? ToBmclapi(Uri original)
    {
        ArgumentNullException.ThrowIfNull(original);
        var rewritten = original.AbsoluteUri
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase)
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase)
            .Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase)
            .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase)
            .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven", StringComparison.OrdinalIgnoreCase)
            .Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets", StringComparison.OrdinalIgnoreCase)
            .Replace("https://maven.minecraftforge.net", "https://bmclapi2.bangbang93.com/maven", StringComparison.OrdinalIgnoreCase)
            .Replace("https://files.minecraftforge.net/maven", "https://bmclapi2.bangbang93.com/maven", StringComparison.OrdinalIgnoreCase)
            .Replace("https://maven.fabricmc.net", "https://bmclapi2.bangbang93.com/maven", StringComparison.OrdinalIgnoreCase)
            .Replace("https://meta.fabricmc.net", "https://bmclapi2.bangbang93.com/fabric-meta", StringComparison.OrdinalIgnoreCase);
        if (original.Host.Equals("maven.neoforged.net", StringComparison.OrdinalIgnoreCase))
        {
            var path = original.AbsolutePath.StartsWith("/releases/", StringComparison.Ordinal)
                ? original.AbsolutePath["/releases".Length..]
                : original.AbsolutePath;
            rewritten = $"https://bmclapi2.bangbang93.com/maven{path}{original.Query}";
        }

        return Uri.TryCreate(rewritten, UriKind.Absolute, out var uri) && uri != original ? uri : null;
    }

    public static bool IsMirror(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.Host.Equals("bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("mod.mcimirror.top", StringComparison.OrdinalIgnoreCase);
    }

    public static Uri? ToCommunityMirror(Uri original)
    {
        ArgumentNullException.ThrowIfNull(original);
        var rewritten = original.AbsoluteUri
            .Replace("https://api.modrinth.com", "https://mod.mcimirror.top/modrinth", StringComparison.OrdinalIgnoreCase)
            .Replace("https://cdn.modrinth.com", "https://mod.mcimirror.top", StringComparison.OrdinalIgnoreCase)
            .Replace("https://api.curseforge.com", "https://mod.mcimirror.top/curseforge", StringComparison.OrdinalIgnoreCase)
            .Replace("https://edge.forgecdn.net", "https://mod.mcimirror.top", StringComparison.OrdinalIgnoreCase);
        return Uri.TryCreate(rewritten, UriKind.Absolute, out var uri) && uri != original ? uri : null;
    }
}
