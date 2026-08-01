namespace PCL.Aurora.Domain;

public static class MinecraftWindowTitleFormatter
{
    public static string Format(string template, MinecraftInstance instance, MinecraftAccount? account)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(instance);
        return template
            .Replace("{name}", instance.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{user}", account?.DisplayName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{login}", GetLoginName(account), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLoginName(MinecraftAccount? account) => account?.Kind switch
    {
        MinecraftAccountKind.Offline => "离线",
        MinecraftAccountKind.Microsoft => "正版",
        MinecraftAccountKind.AuthlibInjector => "Authlib-Injector",
        _ => string.Empty,
    };
}
