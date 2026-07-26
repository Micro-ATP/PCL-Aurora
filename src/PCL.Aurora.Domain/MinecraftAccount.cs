namespace PCL.Aurora.Domain;

public sealed record MinecraftAccount(
    string DisplayName,
    string Uuid,
    MinecraftAccountKind Kind,
    bool IsAuthenticated)
{
    /// <summary>
    /// 仅在当前进程内使用的 Minecraft 服务访问令牌。不得写入普通偏好、日志或界面绑定。
    /// </summary>
    public string? AccessToken { get; init; }

    public override string ToString() =>
        $"{nameof(MinecraftAccount)} {{ DisplayName = {DisplayName}, Uuid = {Uuid}, Kind = {Kind}, IsAuthenticated = {IsAuthenticated} }}";
}
