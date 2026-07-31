namespace PCL.Aurora.Domain;

public static class MinecraftJavaExecutableResolver
{
    public static string Resolve(string executablePath, bool useConsoleExecutable, bool isWindows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!isWindows)
        {
            return executablePath;
        }

        var fileName = Path.GetFileName(executablePath);
        var desiredFileName = useConsoleExecutable ? "java.exe" : "javaw.exe";
        if (string.Equals(fileName, desiredFileName, StringComparison.OrdinalIgnoreCase))
        {
            return executablePath;
        }

        var sibling = Path.Combine(Path.GetDirectoryName(executablePath) ?? string.Empty, desiredFileName);
        return File.Exists(sibling) ? sibling : executablePath;
    }
}
