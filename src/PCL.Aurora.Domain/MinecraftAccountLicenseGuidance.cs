namespace PCL.Aurora.Domain;

/// <summary>
/// 对无法证明 Microsoft 正版所有权的账户给出购买与上游赞助劝导。
/// 用户确认仅适用于当前启动会话，不能替代正版认证。
/// </summary>
public sealed record MinecraftAccountLicenseGuidance(
    bool RequiresAcknowledgement,
    string Message,
    Uri? PurchaseUri)
{
    public const string MinecraftPurchaseUri = "https://www.minecraft.net/store/minecraft-java-bedrock-edition-java-edition";
    public const string PclSponsorUri = "https://meloong.com/afd/a/LTCat";

    public static MinecraftAccountLicenseGuidance Evaluate(MinecraftAccount? account)
    {
        if (account is { Kind: MinecraftAccountKind.Microsoft, IsAuthenticated: true })
        {
            return new(false, "当前使用已认证的 Microsoft 账户。", null);
        }

        if (account is null || !account.IsAuthenticated)
        {
            return new(false, "请先选择可用账户。", null);
        }

        var accountLabel = account.Kind switch
        {
            MinecraftAccountKind.Offline => "离线账户",
            MinecraftAccountKind.AuthlibInjector => "第三方认证账户",
            _ => "未验证账户",
        };
        return new(
            true,
            $"当前使用{accountLabel}，它不能证明你拥有 Minecraft Java 版。请购买正版后使用 Microsoft 账户登录；也请支持 PCL/PCL-CE 原作者。",
            new Uri(MinecraftPurchaseUri));
    }
}
