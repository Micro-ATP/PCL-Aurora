namespace PCL.Aurora.Domain;

public enum MinecraftLaunchRuleAction
{
    Allow,
    Disallow,
}

public sealed record MinecraftLaunchRule(
    MinecraftLaunchRuleAction Action,
    MinecraftLaunchRuleOperatingSystem? OperatingSystem,
    IReadOnlyDictionary<string, bool>? Features);

public sealed record MinecraftLaunchRuleOperatingSystem(
    string? Name,
    string? Version,
    string? Architecture);
