namespace PCL.Aurora.Domain;

/// <summary>
/// 从平台抽象传给版本规则求值器的只读运行环境。
/// </summary>
public sealed record MinecraftLaunchRuleEnvironment(
    string OperatingSystemName,
    string? OperatingSystemVersion,
    string? Architecture,
    IReadOnlyDictionary<string, bool>? Features = null);
