// Directly adapted from PCL-CE, Plain Craft Launcher 2/Modules/Minecraft/ModDownload.cs.
// Copyright © 成都瓜皮龙科技有限公司.
// Modified by Micro-ATP for PCL Aurora: separated the pure Forge/NeoForge
// value objects from PCL's WPF, downloader and installer implementations.
// See LICENSES/PCL-CE-Plain-Craft-Launcher-2-LICENCE.txt and NOTICE.

namespace PCL.Aurora.Domain;

public abstract class PclCeForgelikeEntry : IComparable<PclCeForgelikeEntry>
{
    public enum ForgelikeType
    {
        Forge,
        NeoForge,
        Cleanroom,
    }

    public ForgelikeType forgeType;
    public Version version = new(0, 0);
    public string Inherit = string.Empty;
    public string VersionName = string.Empty;

    public string LoaderName => forgeType.ToString();

    public string FileExtension => forgeType == ForgelikeType.Forge
        ? ((PclCeForgeVersionEntry)this).Category == "installer" ? "jar" : "zip"
        : "jar";

    public bool IsLegacy => forgeType switch
    {
        ForgelikeType.Cleanroom => false,
        _ => version.Major < 20,
    };

    public int CompareTo(PclCeForgelikeEntry? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (version != other.version)
        {
            return version.CompareTo(other.version);
        }

        return PclCeVersionComparer.CompareVersion(VersionName, other.VersionName);
    }
}

public sealed class PclCeForgeVersionEntry : PclCeForgelikeEntry
{
    public string Category = "installer";
    public string FileVersion = string.Empty;
    public string Hash = string.Empty;
    public bool IsRecommended;
    public string ReleaseTime = string.Empty;

    public PclCeForgeVersionEntry(string version, string? branch, string inherit)
    {
        if (version is "11.15.1.2318" or "11.15.1.1902" or "11.15.1.1890")
        {
            branch = "1.8.9";
        }

        if (branch is null && inherit == "1.7.10" && double.Parse(version.Split('.')[3]) >= 1300d)
        {
            branch = "1.7.10";
        }

        forgeType = ForgelikeType.Forge;
        VersionName = version;
        this.version = new Version(version);
        Inherit = inherit;
        FileVersion = version + (branch is null ? string.Empty : "-" + branch);
    }
}

public sealed class PclCeNeoForgeListEntry : PclCeForgelikeEntry
{
    public string ApiName { get; }
    public bool IsBeta { get; }

    public PclCeNeoForgeListEntry(string apiName)
    {
        forgeType = ForgelikeType.NeoForge;
        ApiName = apiName;
        IsBeta = apiName.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
                 apiName.Contains("alpha", StringComparison.OrdinalIgnoreCase);
        if (apiName.Contains("1.20.1", StringComparison.Ordinal))
        {
            VersionName = apiName.Replace("1.20.1-", string.Empty, StringComparison.Ordinal);
            version = new Version("19." + VersionName);
            Inherit = "1.20.1";
        }
        else if (apiName.StartsWith("0.", StringComparison.Ordinal))
        {
            VersionName = apiName;
            var segments = apiName.Split('-', 2)[0].Split('.');
            version = new Version(0, 0, int.Parse(segments[^1]));
            Inherit = segments[1];
        }
        else
        {
            VersionName = apiName;
            version = new Version(apiName.Split('-', 2)[0]);
            Inherit = version.Major >= 24
                ? $"{version.Major}.{version.Minor}{(version.Build > 0 ? $".{version.Build}" : string.Empty)}"
                : "1." + version.Major + (version.Minor > 0 ? "." + version.Minor : string.Empty);
            if (VersionName.Contains('+', StringComparison.Ordinal))
            {
                Inherit += "-" + VersionName[(VersionName.IndexOf('+') + 1)..];
            }
        }
    }

    public string UrlBase
    {
        get
        {
            var packageName = IsLegacy ? "forge" : "neoforge";
            return $"https://maven.neoforged.net/releases/net/neoforged/{packageName}/{ApiName}/{packageName}-{ApiName}";
        }
    }
}
