// Category localization and compatibility summaries directly adapt PCL-CE
// Plain Craft Launcher 2/Modules/Minecraft/ModComp.cs. Modified by Micro-ATP
// for the Aurora domain model and Modrinth-only cross-platform catalog.
using System.Globalization;

namespace PCL.Aurora.Domain;

public sealed record CommunityResourceProject(
    string Id,
    string Slug,
    string Title,
    string Description,
    string Author,
    CommunityResourceType Type,
    Uri WebsiteUrl,
    Uri? IconUrl,
    long Downloads,
    long Followers,
    DateTimeOffset? LastUpdated,
    string? LatestVersion,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> GameVersions)
{
    private static readonly IReadOnlyDictionary<string, string> CategoryTranslations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["technology"] = "科技",
            ["magic"] = "魔法",
            ["adventure"] = "冒险",
            ["utility"] = "实用",
            ["optimization"] = "性能优化",
            ["vanilla-like"] = "原版风",
            ["realistic"] = "写实风",
            ["worldgen"] = "世界元素",
            ["food"] = "食物与烹饪",
            ["game-mechanics"] = "游戏机制",
            ["transportation"] = "运输",
            ["storage"] = "仓储",
            ["social"] = "服务器",
            ["library"] = "支持库",
            ["decoration"] = "装饰",
            ["mobs"] = "生物",
            ["equipment"] = "装备与工具",
            ["multiplayer"] = "多人",
            ["challenging"] = "硬核",
            ["combat"] = "战斗",
            ["quests"] = "任务",
            ["kitchen-sink"] = "水槽包",
            ["lightweight"] = "轻量整合",
            ["simplistic"] = "简洁",
            ["tweaks"] = "改良",
            ["8x-"] = "8x 或更低",
            ["16x"] = "16x",
            ["32x"] = "32x",
            ["48x"] = "48x",
            ["64x"] = "64x",
            ["128x"] = "128x",
            ["256x"] = "256x",
            ["512x+"] = "512x 或更高",
            ["audio"] = "含声音",
            ["fonts"] = "含字体",
            ["models"] = "含模型",
            ["gui"] = "含 UI",
            ["locale"] = "含语言",
            ["core-shaders"] = "核心着色器",
            ["modded"] = "兼容模组",
            ["fantasy"] = "幻想风",
            ["semi-realistic"] = "半写实风",
            ["cartoon"] = "卡通风",
            ["colored-lighting"] = "彩色光照",
            ["path-tracing"] = "路径追踪",
            ["pbr"] = "PBR",
            ["reflections"] = "反射",
        };

    private static readonly IReadOnlyDictionary<string, string> LoaderNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["forge"] = "Forge",
            ["fabric"] = "Fabric",
            ["quilt"] = "Quilt",
            ["neoforge"] = "NeoForge",
            ["iris"] = "Iris",
            ["optifine"] = "OptiFine",
            ["vanilla"] = "原版",
        };

    public IReadOnlyList<string> Loaders { get; init; } = [];

    public string Initial => string.IsNullOrWhiteSpace(Title)
        ? "?"
        : StringInfo.GetNextTextElement(Title.Trim()).ToUpper(CultureInfo.CurrentCulture);

    public string DownloadCountDisplay => Downloads.ToString("N0", CultureInfo.CurrentCulture);

    public string CompactDownloadCountDisplay => Downloads switch
    {
        > 1_000_000_000_000 => $"{Downloads / 1_000_000_000_000d:N2} 万亿",
        > 100_000_000 => $"{Downloads / 100_000_000d:N2} 亿",
        > 100_000 => $"{Math.Round(Downloads / 10_000d):N0} 万",
        _ => DownloadCountDisplay,
    };

    public string FollowerCountDisplay => Followers.ToString("N0", CultureInfo.CurrentCulture);

    public string LastUpdatedDisplay => LastUpdated?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) ?? "未知";

    public string LastUpdatedRelativeDisplay
    {
        get
        {
            if (LastUpdated is not { } lastUpdated)
            {
                return "未知";
            }

            var elapsed = DateTimeOffset.Now - lastUpdated.ToLocalTime();
            if (elapsed < TimeSpan.Zero)
            {
                return "刚刚";
            }

            return elapsed.TotalDays switch
            {
                >= 365 => $"{Math.Max(1, (int)(elapsed.TotalDays / 365)):N0} 年前",
                >= 30 => $"{Math.Max(1, (int)(elapsed.TotalDays / 30)):N0} 个月前",
                >= 1 => $"{Math.Max(1, (int)elapsed.TotalDays):N0} 天前",
                _ when elapsed.TotalHours >= 1 => $"{Math.Max(1, (int)elapsed.TotalHours):N0} 小时前",
                _ => "刚刚",
            };
        }
    }

    public string SourceDisplay => "Modrinth";

    public IReadOnlyList<string> CategoryTags => Categories
        .Where(category => !LoaderNames.ContainsKey(category) &&
                           !category.Equals("datapack", StringComparison.OrdinalIgnoreCase))
        .Select(category => CategoryTranslations.GetValueOrDefault(category))
        .Where(category => !string.IsNullOrWhiteSpace(category))
        .Select(category => category!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.CurrentCulture)
        .ToArray();

    public string CompatibilityDisplay
    {
        get
        {
            var loaderDisplay = Loaders
                .Select(loader => LoaderNames.GetValueOrDefault(loader, loader))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var versionDisplay = GetGameVersionRangeDisplay();
            return loaderDisplay.Length == 0
                ? versionDisplay
                : $"{string.Join(" / ", loaderDisplay)} {versionDisplay}";
        }
    }

    public string CategorySummary => Categories.Count == 0 ? "未标注分类" : string.Join(" · ", Categories.Take(4));

    public string GameVersionSummary => GameVersions.Count == 0
        ? "未标注游戏版本"
        : string.Join(" · ", GameVersions.Take(4));

    private string GetGameVersionRangeDisplay()
    {
        var stableVersions = GameVersions
            .Where(IsStableVersion)
            .Select(GetMinorVersion)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(new PclCeVersionComparer.VersionComparer())
            .ToArray();
        if (stableVersions.Length == 0)
        {
            return GameVersions.Count == 0 ? "未知版本" : "仅快照";
        }

        return stableVersions.Length >= 3
            ? $"{stableVersions[0]}+"
            : string.Join(" / ", stableVersions);
    }

    private static bool IsStableVersion(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts is { Length: >= 2 } && parts.All(part => part.All(char.IsAsciiDigit));
    }

    private static string GetMinorVersion(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 && parts[0] == "1" ? $"{parts[0]}.{parts[1]}" : value;
    }
}
