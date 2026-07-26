using System.Text.RegularExpressions;

namespace PCL.Aurora.Domain;

public static partial class MinecraftLaunchArgumentBuilder
{
    public static MinecraftLaunchArgumentPreparation Prepare(
        MinecraftVersionMetadata? metadata,
        MinecraftLaunchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var blockingReasons = new List<string>();
        if (metadata?.Launch is not { } launch)
        {
            return new(null, ["版本元数据未提供启动信息。"]);
        }

        if (!launch.HasModernArguments)
        {
            blockingReasons.Add("旧版 minecraftArguments 启动参数尚未迁移。");
        }

        if (launch.HasConditionalArguments)
        {
            blockingReasons.Add("版本包含条件启动参数，规则评估尚未迁移。");
        }

        if (string.IsNullOrWhiteSpace(launch.MainClass))
        {
            blockingReasons.Add("版本元数据缺少 mainClass。");
        }

        var replacements = CreateReplacements(context);
        var jvmArguments = ReplaceAll(launch.JvmArguments, replacements, blockingReasons);
        var gameArguments = ReplaceAll(launch.GameArguments, replacements, blockingReasons);
        if (blockingReasons.Count > 0 || string.IsNullOrWhiteSpace(launch.MainClass))
        {
            return new(null, blockingReasons);
        }

        return new(new MinecraftLaunchArguments(jvmArguments, launch.MainClass, gameArguments), []);
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
