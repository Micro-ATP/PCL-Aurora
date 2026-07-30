using System.Text.RegularExpressions;

namespace PCL.Aurora.Domain;

public static partial class MinecraftLaunchArgumentBuilder
{
    public static MinecraftLaunchArgumentPreparation Prepare(
        MinecraftVersionMetadata? metadata,
        MinecraftLaunchContext context,
        MinecraftLaunchOptions? launchOptions = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var blockingReasons = new List<string>();
        launchOptions ??= MinecraftLaunchOptions.Default;
        if (!launchOptions.IsValid)
        {
            return new(null, ["自定义启动选项包含不支持的值。"]);
        }

        if (metadata?.Launch is not { } launch)
        {
            return new(null, ["版本元数据未提供启动信息。"]);
        }

        if (launch.HasUnsupportedConditionalArguments ||
            (launch.HasConditionalArguments &&
             launch.ConditionalJvmArguments is null &&
             launch.ConditionalGameArguments is null))
        {
            blockingReasons.Add("版本包含无法安全解析的条件启动参数。");
        }

        if (string.IsNullOrWhiteSpace(launch.MainClass))
        {
            blockingReasons.Add("版本元数据缺少 mainClass。");
        }

        var replacements = CreateReplacements(context);
        var jvmTemplates = SelectConditionalArguments(
            launch.JvmArguments,
            launch.ConditionalJvmArguments,
            context.RuleEnvironment,
            blockingReasons);
        var gameTemplates = SelectConditionalArguments(
            launch.GameArguments,
            launch.ConditionalGameArguments,
            context.RuleEnvironment,
            blockingReasons);
        if (!launch.HasModernArguments && jvmTemplates.Count == 0)
        {
            jvmTemplates = CreateLegacyJvmArguments();
        }

        if (!string.IsNullOrWhiteSpace(launch.LegacyGameArguments))
        {
            gameTemplates = AppendLegacyGameArguments(gameTemplates, launch.LegacyGameArguments, blockingReasons);
        }

        jvmTemplates = AppendCustomArguments(
            jvmTemplates,
            launchOptions.AdditionalJvmArguments,
            "额外 JVM 参数",
            blockingReasons);
        gameTemplates = AppendCustomArguments(
            gameTemplates,
            launchOptions.AdditionalGameArguments,
            "额外游戏参数",
            blockingReasons);
        jvmTemplates = AppendPreferredIpStack(jvmTemplates, launchOptions.PreferredIpStack);
        if (launchOptions.WindowMode == MinecraftGameWindowMode.Fullscreen)
        {
            gameTemplates = gameTemplates.Concat(["--fullscreen"]).ToArray();
        }

        if (context.MaximumMemoryMiB is { } maximumMemoryMiB &&
            maximumMemoryMiB > 0 &&
            !jvmTemplates.Any(argument => argument.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase)))
        {
            jvmTemplates = jvmTemplates.Concat([$"-Xmx{maximumMemoryMiB}M"]).ToArray();
        }
        if (launchOptions.LockMemory &&
            context.MaximumMemoryMiB is { } lockedMemoryMiB &&
            lockedMemoryMiB > 0 &&
            !jvmTemplates.Any(argument => argument.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase)))
        {
            jvmTemplates = jvmTemplates.Concat([$"-Xms{lockedMemoryMiB}M"]).ToArray();
        }

        var jvmArguments = Pcl2MinecraftLaunchArgumentDeduplicator.Deduplicate(
            ReplaceAll(jvmTemplates, replacements, blockingReasons),
            isJvmArgument: true);
        var gameArguments = Pcl2MinecraftLaunchArgumentDeduplicator.Deduplicate(
            ReplaceAll(gameTemplates, replacements, blockingReasons),
            isJvmArgument: false);
        if (blockingReasons.Count > 0 || string.IsNullOrWhiteSpace(launch.MainClass))
        {
            return new(null, blockingReasons);
        }

        return new(new MinecraftLaunchArguments(jvmArguments, launch.MainClass, gameArguments), []);
    }

    private static IReadOnlyList<string> CreateLegacyJvmArguments() =>
    [
        "-Djava.library.path=${natives_directory}",
        "-cp",
        "${classpath}",
    ];

    private static IReadOnlyList<string> AppendPreferredIpStack(
        IReadOnlyList<string> arguments,
        MinecraftPreferredIpStack preferredIpStack) => preferredIpStack switch
    {
        MinecraftPreferredIpStack.PreferIpv4 => arguments.Concat([
            "-Djava.net.preferIPv4Stack=true",
            "-Djava.net.preferIPv4Addresses=true",
        ]).ToArray(),
        MinecraftPreferredIpStack.PreferIpv6 => arguments.Concat([
            "-Djava.net.preferIPv6Stack=true",
            "-Djava.net.preferIPv6Addresses=true",
        ]).ToArray(),
        _ => arguments,
    };

    private static IReadOnlyList<string> AppendLegacyGameArguments(
        IReadOnlyList<string> modernArguments,
        string legacyArguments,
        List<string> blockingReasons)
    {
        if (!Pcl2MinecraftLegacyArgumentTokenizer.TryTokenize(legacyArguments, out var legacyTokens, out var error))
        {
            blockingReasons.Add(error!);
            return modernArguments;
        }

        var result = new List<string>(legacyTokens.Count + modernArguments.Count + 4);
        result.AddRange(legacyTokens);
        if (!legacyTokens.Contains("--height", StringComparer.Ordinal))
        {
            result.Add("--height");
            result.Add("${resolution_height}");
            result.Add("--width");
            result.Add("${resolution_width}");
        }

        result.AddRange(modernArguments);
        return result;
    }

    private static IReadOnlyList<string> AppendCustomArguments(
        IReadOnlyList<string> baseArguments,
        string? customArguments,
        string settingName,
        List<string> blockingReasons)
    {
        if (string.IsNullOrWhiteSpace(customArguments))
        {
            return baseArguments;
        }

        if (!Pcl2MinecraftLegacyArgumentTokenizer.TryTokenize(customArguments, out var customTokens, out var error))
        {
            blockingReasons.Add($"{settingName}：{error}");
            return baseArguments;
        }

        return baseArguments.Concat(customTokens).ToArray();
    }

    private static IReadOnlyList<string> SelectConditionalArguments(
        IReadOnlyList<string> unconditionalArguments,
        IReadOnlyList<MinecraftConditionalLaunchArgument>? conditionalArguments,
        MinecraftLaunchRuleEnvironment? environment,
        List<string> blockingReasons)
    {
        if (conditionalArguments is null || conditionalArguments.Count == 0)
        {
            return unconditionalArguments;
        }

        if (environment is null)
        {
            blockingReasons.Add("版本包含条件启动参数，但未提供规则执行环境。");
            return unconditionalArguments;
        }

        var result = new List<string>(unconditionalArguments);
        foreach (var conditional in conditionalArguments)
        {
            if (PclCeMinecraftLaunchRuleEvaluator.IsAllowed(conditional.Rules, environment))
            {
                result.AddRange(conditional.Values);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ReplaceAll(
        IReadOnlyList<string> templates,
        IReadOnlyDictionary<string, string?> replacements,
        List<string> blockingReasons)
    {
        var result = new List<string>(templates.Count);
        foreach (var template in templates)
        {
            var argument = template;
            foreach (Match placeholder in PlaceholderPattern().Matches(template))
            {
                var token = placeholder.Value;
                if (!replacements.TryGetValue(token, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    blockingReasons.Add($"启动参数需要 {token}，但该值尚未准备。");
                    continue;
                }

                argument = argument.Replace(token, value, StringComparison.Ordinal);
            }

            result.Add(argument);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string?> CreateReplacements(MinecraftLaunchContext context) =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["${classpath_separator}"] = Path.PathSeparator.ToString(),
            ["${classpath}"] = context.Classpath,
            ["${natives_directory}"] = context.NativesDirectory,
            ["${game_directory}"] = context.GameDirectory,
            ["${assets_root}"] = context.AssetsRoot,
            ["${game_assets}"] = context.AssetsRoot is null
                ? null
                : Path.Combine(context.AssetsRoot, "virtual", "legacy"),
            ["${assets_index_name}"] = context.AssetsIndexName,
            ["${launcher_name}"] = context.LauncherName,
            ["${launcher_version}"] = context.LauncherVersion,
            ["${version_name}"] = context.VersionName,
            ["${version_type}"] = context.VersionType,
            ["${auth_player_name}"] = context.Account?.DisplayName,
            ["${auth_uuid}"] = context.Account?.Uuid,
            ["${auth_access_token}"] = GetAccessToken(context.Account),
            ["${access_token}"] = GetAccessToken(context.Account),
            ["${auth_session}"] = GetAccessToken(context.Account),
            ["${user_type}"] = context.Account?.Kind switch
            {
                MinecraftAccountKind.Offline => "legacy",
                MinecraftAccountKind.Microsoft when context.Account.IsAuthenticated && !string.IsNullOrWhiteSpace(context.Account.AccessToken) => "msa",
                _ => null,
            },
            ["${user_properties}"] = "{}",
            ["${resolution_width}"] = context.ResolutionWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["${resolution_height}"] = context.ResolutionHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

    private static string? GetAccessToken(MinecraftAccount? account) => account?.Kind switch
    {
        MinecraftAccountKind.Offline => "0",
        MinecraftAccountKind.Microsoft when account.IsAuthenticated && !string.IsNullOrWhiteSpace(account.AccessToken) => account.AccessToken,
        _ => null,
    };

    [GeneratedRegex(@"\$\{[A-Za-z0-9_]+\}")]
    private static partial Regex PlaceholderPattern();
}
