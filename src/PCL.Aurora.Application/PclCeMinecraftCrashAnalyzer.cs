namespace PCL.Aurora.Application;

/// <summary>
/// Adapts the high-signal crash categories used by PCL-CE's CrashAnalyzer to
/// Aurora's captured cross-platform process output.
/// </summary>
public static class PclCeMinecraftCrashAnalyzer
{
    public static MinecraftCrashAnalysisResult Analyze(int exitCode, IReadOnlyList<string> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var text = string.Join('\n', output.TakeLast(4000));
        if (ContainsAny(text, "OutOfMemoryError", "Java heap space", "GC overhead limit exceeded"))
        {
            return Result("可用内存不足或分配给游戏的内存过小。", output, "OutOfMemory", "heap space");
        }
        if (ContainsAny(text, "Could not reserve enough space", "Invalid maximum heap size"))
        {
            return Result("Java 无法申请当前设置的内存，请降低内存或检查 Java 架构。", output, "reserve enough space", "maximum heap size");
        }
        if (ContainsAny(text, "UnsupportedClassVersionError", "class file version"))
        {
            return Result("当前 Java 版本与游戏或模组要求不匹配。", output, "UnsupportedClassVersionError", "class file version");
        }
        if (ContainsAny(text, "GLFW error 65542", "does not support OpenGL", "OpenGL context"))
        {
            return Result("显卡驱动或 OpenGL 支持异常。", output, "GLFW error", "OpenGL");
        }
        if (ContainsAny(text, "EXCEPTION_ACCESS_VIOLATION", "SIGSEGV", "A fatal error has been detected by the Java Runtime Environment"))
        {
            return Result("Java 或本地库发生原生崩溃，请检查显卡驱动、Java 与模组兼容性。", output, "EXCEPTION_ACCESS_VIOLATION", "SIGSEGV", "fatal error");
        }
        if (ContainsAny(text, "Mixin apply failed", "MixinTransformerError", "Mixin transformation"))
        {
            return Result("模组 Mixin 注入失败，通常由模组冲突或版本不匹配引起。", output, "Mixin");
        }
        if (ContainsAny(text, "NoClassDefFoundError", "ClassNotFoundException", "NoSuchMethodError"))
        {
            return Result("缺少依赖或模组版本不兼容。", output, "NoClassDefFoundError", "ClassNotFoundException", "NoSuchMethodError");
        }
        if (ContainsAny(text, "Failed to download file", "Couldn't download", "Hash check failed"))
        {
            return Result("游戏文件下载不完整或校验失败。", output, "download", "Hash check failed");
        }
        if (ContainsAny(text, "Authentication servers are down", "Invalid credentials", "Failed to verify username"))
        {
            return Result("登录验证失败或认证服务暂时不可用。", output, "Authentication", "credentials", "verify username");
        }

        return new(
            $"游戏以异常代码 {exitCode} 退出，但现有日志中没有识别到明确原因。",
            output.TakeLast(3).ToArray());
    }

    private static MinecraftCrashAnalysisResult Result(
        string summary,
        IReadOnlyList<string> output,
        params string[] markers) =>
        new(
            summary,
            output.Where(line => markers.Any(marker => line.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                .TakeLast(3)
                .ToArray());

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
}
