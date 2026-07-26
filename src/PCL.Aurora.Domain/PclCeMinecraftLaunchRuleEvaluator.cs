// Directly adapted from PCL-CE, Plain Craft Launcher 2/Modules/Minecraft/ModLibrary.cs.
// Copyright © 成都瓜皮龙科技有限公司.
// Modified by Micro-ATP for PCL Aurora: retains ordered allow/disallow semantics while
// receiving macOS/Linux/Windows facts through a cross-platform value object instead of Windows globals.
// See LICENSES/PCL-CE-Plain-Craft-Launcher-2-LICENCE.txt and NOTICE.

using System.Text.RegularExpressions;

namespace PCL.Aurora.Domain;

/// <summary>
/// 求值 Minecraft 版本 JSON 的 rules。后一个命中的 allow/disallow 规则覆盖前一个。
/// </summary>
public static class PclCeMinecraftLaunchRuleEvaluator
{
    public static bool IsAllowed(
        IReadOnlyList<MinecraftLaunchRule>? rules,
        MinecraftLaunchRuleEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (rules is null)
        {
            return true;
        }

        var required = false;
        foreach (var rule in rules)
        {
            if (Matches(rule, environment))
            {
                required = rule.Action == MinecraftLaunchRuleAction.Allow;
            }
        }

        return required;
    }

    private static bool Matches(MinecraftLaunchRule rule, MinecraftLaunchRuleEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!MatchesOperatingSystem(rule.OperatingSystem, environment))
        {
            return false;
        }

        if (rule.Features is null)
        {
            return true;
        }

        foreach (var feature in rule.Features)
        {
            var actual = environment.Features is not null &&
                         environment.Features.TryGetValue(feature.Key, out var value) &&
                         value;
            if (actual != feature.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesOperatingSystem(
        MinecraftLaunchRuleOperatingSystem? operatingSystem,
        MinecraftLaunchRuleEnvironment environment)
    {
        if (operatingSystem is null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(operatingSystem.Name) &&
            !string.Equals(operatingSystem.Name, "unknown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(operatingSystem.Name, environment.OperatingSystemName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(operatingSystem.Architecture) &&
            !string.Equals(operatingSystem.Architecture, environment.Architecture, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(operatingSystem.Version))
        {
            return true;
        }

        try
        {
            return Regex.IsMatch(environment.OperatingSystemVersion ?? string.Empty, operatingSystem.Version);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
