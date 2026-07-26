namespace PCL.Aurora.Domain;

/// <summary>
/// 已通过加载器安装计划校验、可交由受控 Java 进程执行的参数。
/// 参数必须逐项传入 ProcessStartInfo.ArgumentList，不能拼接为 shell 命令。
/// </summary>
public sealed record MinecraftLoaderInstallerProcessRequest(
    string JavaExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> ArgumentList);
