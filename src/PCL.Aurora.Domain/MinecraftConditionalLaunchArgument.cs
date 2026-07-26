namespace PCL.Aurora.Domain;

/// <summary>
/// 版本 JSON 中带有可选 <c>rules</c> 的启动参数对象。
/// <see cref="Rules"/> 为 <see langword="null"/> 表示该对象没有 rules 字段，必须直接保留。
/// </summary>
public sealed record MinecraftConditionalLaunchArgument(
    IReadOnlyList<string> Values,
    IReadOnlyList<MinecraftLaunchRule>? Rules);
