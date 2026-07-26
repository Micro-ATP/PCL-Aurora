namespace PCL.Aurora.Domain;

public sealed record LaunchReadiness(bool CanLaunch, IReadOnlyList<string> BlockingReasons)
{
    public static LaunchReadiness Evaluate(
        MinecraftInstance? instance,
        MinecraftAccount? account,
        JavaInstallation? java)
    {
        var reasons = new List<string>();
        if (instance is null || instance.Status != MinecraftInstanceStatus.Valid)
        {
            reasons.Add("未选择有效的 Minecraft 实例。");
        }

        if (account is null || !account.IsAuthenticated)
        {
            reasons.Add("未选择可用账户。");
        }

        if (java is null || !java.IsCompatible)
        {
            reasons.Add("未找到兼容的 Java。");
        }

        return new LaunchReadiness(reasons.Count == 0, reasons);
    }
}
