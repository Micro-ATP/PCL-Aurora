// Directly adapted from PCL2, Plain Craft Launcher 2/Modules/Minecraft/ModJava.vb.
// Copyright © 成都瓜皮龙科技有限公司.
// Modified by Micro-ATP for PCL Aurora: uses standard version metadata and release dates
// only; loader-specific limits and Java download behavior remain separate platform work.
// See LICENSES/PCL2-LICENCE.txt and NOTICE.

namespace PCL.Aurora.Domain;

public static class Pcl2MinecraftJavaRequirementEvaluator
{
    private static readonly DateTimeOffset Java21Cutoff = new(2024, 4, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Java17Cutoff = new(2021, 11, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Java16Cutoff = new(2021, 5, 11, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Java8MaximumCutoff = new(2013, 5, 1, 0, 0, 0, TimeSpan.Zero);

    public static MinecraftJavaRequirement Evaluate(MinecraftVersionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.JavaVersionRequirement is { IsValid: true } declaredRequirement)
        {
            return new(
                MinimumMajorVersion: declaredRequirement.MajorVersion,
                MaximumMajorVersion: null,
                declaredRequirement.Component,
                "版本 JSON 的 javaVersion.majorVersion");
        }

        var releaseTime = metadata.ReleaseTime;
        if (releaseTime is null)
        {
            return new(null, null, null, "版本元数据未提供 Java 版本要求或发布日期");
        }

        if (releaseTime >= Java21Cutoff)
        {
            return new(21, null, null, "PCL2 的 1.20.5+ 发布日期回退规则");
        }

        if (releaseTime >= Java17Cutoff)
        {
            return new(17, null, null, "PCL2 的 1.18+ 发布日期回退规则");
        }

        if (releaseTime >= Java16Cutoff)
        {
            return new(16, null, null, "PCL2 的 1.17+ 发布日期回退规则");
        }

        if (releaseTime.Value.Year >= 2017)
        {
            return new(8, null, null, "PCL2 的 1.12+ 发布日期回退规则");
        }

        if (releaseTime.Value.Year >= 2001 && releaseTime <= Java8MaximumCutoff)
        {
            return new(null, 8, null, "PCL2 的 1.5.2 及更早版本发布日期回退规则");
        }

        return new(null, null, null, "版本发布日期未落入可验证的 PCL2 Java 回退区间");
    }
}
