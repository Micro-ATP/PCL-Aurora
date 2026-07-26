// Directly adapted from PCL2, Plain Craft Launcher 2/Modules/Minecraft/ModMinecraft.vb.
// Copyright © 成都瓜皮龙科技有限公司.
// Modified by Micro-ATP for PCL Aurora: keeps PCL2's Maven/assets mirror mapping
// only when the existing plan carries a valid SHA-1, and always retains the official URL.
// See LICENSES/PCL2-LICENCE.txt and NOTICE.

namespace PCL.Aurora.Domain;

public static class Pcl2VerifiedMirrorSourceMapper
{
    public static MinecraftDownloadArtifact PreferMirrorWhenVerified(MinecraftDownloadArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (!HasSha1(artifact.Sha1) || !TryCreateMirrorUri(artifact.Url, out var mirrorUri))
        {
            return artifact;
        }

        var officialSources = new[] { artifact.Url }
            .Concat(artifact.AlternativeUrls ?? [])
            .Where(uri => !Uri.Compare(uri, mirrorUri, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase).Equals(0))
            .Distinct()
            .ToArray();
        return artifact with { Url = mirrorUri, AlternativeUrls = officialSources };
    }

    private static bool TryCreateMirrorUri(Uri source, out Uri mirrorUri)
    {
        mirrorUri = null!;
        if (source.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var path = source.AbsolutePath;
        if (source.Host.Equals("resources.download.minecraft.net", StringComparison.OrdinalIgnoreCase))
        {
            mirrorUri = new Uri($"https://bmclapi2.bangbang93.com/assets{path}");
            return true;
        }

        if (source.Host.Equals("libraries.minecraft.net", StringComparison.OrdinalIgnoreCase))
        {
            mirrorUri = new Uri($"https://bmclapi2.bangbang93.com/maven{path}");
            return true;
        }

        if (!source.Host.Equals("maven.fabricmc.net", StringComparison.OrdinalIgnoreCase) &&
            !source.Host.Equals("maven.minecraftforge.net", StringComparison.OrdinalIgnoreCase) &&
            !source.Host.Equals("maven.neoforged.net", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (source.Host.Equals("maven.neoforged.net", StringComparison.OrdinalIgnoreCase) &&
            path.StartsWith("/releases/", StringComparison.Ordinal))
        {
            path = path["/releases".Length..];
        }

        mirrorUri = new Uri($"https://bmclapi2.bangbang93.com/maven{path}");
        return true;
    }

    private static bool HasSha1(string? value) =>
        value is { Length: 40 } && value.All(Uri.IsHexDigit);
}
