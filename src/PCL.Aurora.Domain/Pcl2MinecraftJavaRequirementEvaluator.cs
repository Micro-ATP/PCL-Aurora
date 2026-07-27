// Directly adapted from PCL2, Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb.
// Copyright © 成都瓜皮龙科技有限公司.
// Modified by Micro-ATP for PCL Aurora: evaluates only local metadata and installed
// loader facts; omits PCL's Windows UI, Java download behavior and unknown-loader guesses.
// See LICENSES/PCL2-LICENCE.txt and NOTICE.

namespace PCL.Aurora.Domain;

public static class Pcl2MinecraftJavaRequirementEvaluator
{
    private static readonly DateTimeOffset Java21Cutoff = new(2024, 4, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Java17Cutoff = new(2021, 11, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Java16Cutoff = new(2021, 5, 11, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Java8MaximumCutoff = new(2013, 5, 1, 0, 0, 0, TimeSpan.Zero);

    public static MinecraftJavaRequirement Evaluate(
        MinecraftVersionMetadata metadata,
        MinecraftInstance? instance = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var requirements = new RequirementAccumulator();
        AddVanillaRequirements(requirements, metadata);
        AddLoaderRequirements(requirements, metadata, instance);
        return requirements.Build();
    }

    private static void AddVanillaRequirements(RequirementAccumulator requirements, MinecraftVersionMetadata metadata)
    {
        if (metadata.JavaVersionRequirement is { IsValid: true } declaredRequirement)
        {
            requirements.AddMinimumMajor(
                declaredRequirement.MajorVersion,
                "版本 JSON 的 javaVersion.majorVersion");
            requirements.RecommendedComponent = declaredRequirement.Component;
            return;
        }

        var releaseTime = metadata.ReleaseTime;
        if (releaseTime is null)
        {
            requirements.AddSource("版本元数据未提供 Java 版本要求或发布日期");
            return;
        }

        if (releaseTime >= Java21Cutoff)
        {
            requirements.AddMinimumMajor(21, "PCL2 的 1.20.5+ 发布日期回退规则");
        }
        else if (releaseTime >= Java17Cutoff)
        {
            requirements.AddMinimumMajor(17, "PCL2 的 1.18+ 发布日期回退规则");
        }
        else if (releaseTime >= Java16Cutoff)
        {
            requirements.AddMinimumMajor(16, "PCL2 的 1.17+ 发布日期回退规则");
        }
        else if (releaseTime.Value.Year >= 2017)
        {
            requirements.AddMinimumMajor(8, "PCL2 的 1.12+ 发布日期回退规则");
        }
        else if (releaseTime.Value.Year >= 2001 && releaseTime <= Java8MaximumCutoff)
        {
            requirements.AddMaximumMajor(8, "PCL2 的 1.5.2 及更早版本发布日期回退规则");
        }
        else
        {
            requirements.AddSource("版本发布日期未落入可验证的 PCL2 Java 回退区间");
        }
    }

    private static void AddLoaderRequirements(
        RequirementAccumulator requirements,
        MinecraftVersionMetadata metadata,
        MinecraftInstance? instance)
    {
        if (instance is null)
        {
            return;
        }

        var vanillaVersion = GetVanillaVersion(metadata, instance);
        if (instance.HasOptiFine)
        {
            AddOptiFineRequirements(requirements, vanillaVersion);
        }

        switch (instance.InstalledLoader)
        {
            case { Kind: MinecraftLoaderKind.Forge } forge:
                AddForgeRequirements(requirements, forge, vanillaVersion, instance.HasOptiFine);
                break;
            case { Kind: MinecraftLoaderKind.NeoForge } neoForge:
                AddNeoForgeRequirements(requirements, neoForge, vanillaVersion);
                break;
            case { Kind: MinecraftLoaderKind.Fabric }:
                AddFabricRequirements(requirements, vanillaVersion);
                break;
        }
    }

    private static void AddOptiFineRequirements(RequirementAccumulator requirements, string? vanillaVersion)
    {
        var minor = GetMinecraftMinorVersion(vanillaVersion);
        if (minor is null)
        {
            return;
        }

        if (minor < 7)
        {
            requirements.AddMaximumMajor(8, "PCL2 的 OptiFine 1.6 及更早版本规则");
        }
        else if (minor is >= 8 and < 12)
        {
            requirements.AddMinimumMajor(8, "PCL2 的 OptiFine 1.8–1.11 规则");
            requirements.AddMaximumMajor(8, "PCL2 的 OptiFine 1.8–1.11 规则");
        }
        else if (minor == 12)
        {
            requirements.AddMaximumMajor(8, "PCL2 的 OptiFine 1.12 规则");
        }
    }

    private static void AddForgeRequirements(
        RequirementAccumulator requirements,
        MinecraftInstalledLoader forge,
        string? vanillaVersion,
        bool hasOptiFine)
    {
        var minor = GetMinecraftMinorVersion(vanillaVersion);
        if (IsMinecraftVersionBetween(vanillaVersion, "1.6.1", "1.7.2"))
        {
            requirements.AddMinimumMajor(7, "PCL2 的 Forge 1.6.1–1.7.2 规则");
            requirements.AddMaximumMajor(7, "PCL2 的 Forge 1.6.1–1.7.2 规则");
        }
        else if (minor is <= 12)
        {
            requirements.AddMaximumMajor(8, "PCL2 的 Forge 1.12 及更早版本规则");
        }
        else if (minor is >= 13 and <= 14)
        {
            requirements.AddMinimumMajor(8, "PCL2 的 Forge 1.13–1.14 规则");
            requirements.AddMaximumMajor(10, "PCL2 的 Forge 1.13–1.14 规则");
        }
        else if (minor == 15)
        {
            requirements.AddMinimumMajor(8, "PCL2 的 Forge 1.15 规则");
            requirements.AddMaximumMajor(15, "PCL2 的 Forge 1.15 规则");
        }
        else if (IsLoaderVersionBetween(forge.Version, "34.0.0", "36.2.25"))
        {
            requirements.AddMaximumVersion(new Version(8, 0, 320), "PCL2 的 Forge 34.0.0–36.2.25 规则");
        }
        else if (IsLoaderVersionBetween(forge.Version, "36.2.26", "36.999999.999999"))
        {
            requirements.AddMaximumMajor(23, "PCL2 的 Forge 36.2.26–36.x 规则");
        }
        else if (IsLoaderVersionBetween(forge.Version, "37.0.0", "37.0.79"))
        {
            requirements.AddMaximumMajor(16, "PCL2 的 Forge 37.0.0–37.0.79 规则");
        }
        else if (minor == 18 && hasOptiFine)
        {
            requirements.AddMaximumMajor(18, "PCL2 的 Forge + OptiFine 1.18 规则");
        }
        else if (IsLoaderVersionBetween(forge.Version, "45.0.21", "45.0.65"))
        {
            requirements.AddMaximumMajor(19, "PCL2 的 Forge 45.0.21–45.0.65 规则");
        }
        else if (IsLoaderVersionBetween(forge.Version, "45.0.66", "47.4.8"))
        {
            requirements.AddMaximumMajor(21, "PCL2 的 Forge 45.0.66–47.4.8 规则");
        }
    }

    private static void AddNeoForgeRequirements(
        RequirementAccumulator requirements,
        MinecraftInstalledLoader neoForge,
        string? vanillaVersion)
    {
        var versionIsWithinEarlyRange = neoForge.Version is { } version &&
            !version.Contains("25w14craftmine", StringComparison.OrdinalIgnoreCase) &&
            PclCeVersionComparer.CompareVersionGe("20.2.62-beta", version);
        if (string.Equals(vanillaVersion, "1.20.1", StringComparison.OrdinalIgnoreCase) || versionIsWithinEarlyRange)
        {
            requirements.AddMaximumMajor(21, "PCL2 的 NeoForge 1.20.1/20.2.62-beta 及更早版本规则");
        }
    }

    private static void AddFabricRequirements(RequirementAccumulator requirements, string? vanillaVersion)
    {
        var minor = GetMinecraftMinorVersion(vanillaVersion);
        if (minor is >= 15 and <= 16)
        {
            requirements.AddMinimumMajor(8, "PCL2 的 Fabric 1.15–1.16 规则");
        }
        else if (minor >= 18)
        {
            requirements.AddMinimumMajor(17, "PCL2 的 Fabric 1.18+ 规则");
        }
    }

    private static string? GetVanillaVersion(MinecraftVersionMetadata metadata, MinecraftInstance instance) =>
        FirstMinecraftReleaseVersion(
            instance.BaseVersionId,
            instance.InstalledLoader?.MinecraftVersion,
            metadata.Id);

    private static string? FirstMinecraftReleaseVersion(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => GetMinecraftMinorVersion(candidate) is not null);

    private static int? GetMinecraftMinorVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var segments = version.Split('.');
        return segments.Length >= 2 &&
               string.Equals(segments[0], "1", StringComparison.Ordinal) &&
               int.TryParse(segments[1], out var minor)
            ? minor
            : null;
    }

    private static bool IsMinecraftVersionBetween(string? value, string lower, string upper) =>
        value is not null &&
        PclCeVersionComparer.CompareVersionGe(value, lower) &&
        PclCeVersionComparer.CompareVersionGe(upper, value);

    private static bool IsLoaderVersionBetween(string? value, string lower, string upper) =>
        value is not null &&
        PclCeVersionComparer.CompareVersionGe(value, lower) &&
        PclCeVersionComparer.CompareVersionGe(upper, value);

    private sealed class RequirementAccumulator
    {
        private readonly List<string> sources = [];

        public int? MinimumMajorVersion { get; private set; }

        public int? MaximumMajorVersion { get; private set; }

        public Version? MinimumVersion { get; private set; }

        public Version? MaximumVersion { get; private set; }

        public string? RecommendedComponent { get; set; }

        public void AddMinimumMajor(int majorVersion, string source)
        {
            MinimumMajorVersion = Math.Max(MinimumMajorVersion ?? majorVersion, majorVersion);
            AddSource(source);
        }

        public void AddMaximumMajor(int majorVersion, string source)
        {
            MaximumMajorVersion = Math.Min(MaximumMajorVersion ?? majorVersion, majorVersion);
            AddSource(source);
        }

        public void AddMaximumVersion(Version version, string source)
        {
            AddMaximumMajor(version.Major, source);
            MaximumVersion = MaximumVersion is null || version < MaximumVersion ? version : MaximumVersion;
        }

        public void AddSource(string source)
        {
            if (!sources.Contains(source, StringComparer.Ordinal))
            {
                sources.Add(source);
            }
        }

        public MinecraftJavaRequirement Build() => new(
            MinimumMajorVersion,
            MaximumMajorVersion,
            RecommendedComponent,
            sources.Count == 0 ? "版本元数据未提供可验证的 Java 版本要求" : string.Join("；", sources),
            MinimumVersion,
            MaximumVersion);
    }
}
