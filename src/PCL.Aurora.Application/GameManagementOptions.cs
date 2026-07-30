namespace PCL.Aurora.Application;

public enum DownloadSourcePreference
{
    Mirror = 0,
    PreferOfficialWithFallback = 1,
    Official = 2,
}

public enum CommunityFileNameFormat
{
    ChineseBrackets = 0,
    SquareBrackets = 1,
    TranslatedNameFirst = 2,
    OriginalNameFirst = 3,
    OriginalNameOnly = 4,
}

public enum CommunityModNameStyle
{
    TranslationTitle = 0,
    FileNameTitle = 1,
}

public enum CommunityQuickDownloadBehavior
{
    AlwaysAsk = 0,
    CurrentInstance = 1,
    AskInstance = 2,
    AskPath = 3,
}

/// <summary>
/// 游戏与社区资源管理设置。字段、顺序和默认值适配自 PCL-CE
/// PageSetupGameManage 与 Config.Download/Config.Tool。
/// </summary>
public sealed record GameManagementOptions(
    DownloadSourcePreference FileSource = DownloadSourcePreference.PreferOfficialWithFallback,
    DownloadSourcePreference VersionListSource = DownloadSourcePreference.PreferOfficialWithFallback,
    bool AutoSelectNewInstance = true,
    bool FixAuthlib = true,
    DownloadSourcePreference CommunitySource = DownloadSourcePreference.PreferOfficialWithFallback,
    CommunityFileNameFormat CommunityFileNameFormat = CommunityFileNameFormat.SquareBrackets,
    CommunityModNameStyle CommunityModNameStyle = CommunityModNameStyle.TranslationTitle,
    CommunityQuickDownloadBehavior QuickDownloadBehavior = CommunityQuickDownloadBehavior.AlwaysAsk,
    bool IgnoreQuilt = true,
    bool AutoInstallDependencies = true,
    bool ReleaseNotifications = false,
    bool SnapshotNotifications = false,
    bool AutoChangeGameLanguage = true,
    bool ReadClipboard = false)
{
    public static GameManagementOptions Default { get; } = new();

    public bool IsValid =>
        Enum.IsDefined(FileSource) &&
        Enum.IsDefined(VersionListSource) &&
        Enum.IsDefined(CommunitySource) &&
        Enum.IsDefined(CommunityFileNameFormat) &&
        Enum.IsDefined(CommunityModNameStyle) &&
        Enum.IsDefined(QuickDownloadBehavior);
}
