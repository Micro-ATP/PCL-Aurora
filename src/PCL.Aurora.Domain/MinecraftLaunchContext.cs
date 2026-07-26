namespace PCL.Aurora.Domain;

public sealed record MinecraftLaunchContext(
    string? Classpath,
    string? NativesDirectory,
    string? GameDirectory,
    string? AssetsRoot,
    string? AssetsIndexName,
    string LauncherName,
    string LauncherVersion,
    string VersionName,
    string? VersionType,
    MinecraftAccount? Account,
    int ResolutionWidth,
    int ResolutionHeight,
    MinecraftLaunchRuleEnvironment? RuleEnvironment = null)
{
    public static MinecraftLaunchContext CreateDefault(string versionName) => new(
        Classpath: null,
        NativesDirectory: null,
        GameDirectory: null,
        AssetsRoot: null,
        AssetsIndexName: null,
        LauncherName: "PCL Aurora",
        LauncherVersion: "0.1.0",
        VersionName: versionName,
        VersionType: null,
        Account: null,
        ResolutionWidth: 854,
        ResolutionHeight: 480);
}
