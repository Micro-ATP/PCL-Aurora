namespace PCL.Aurora.Domain;

public sealed record MinecraftGameLaunchRequest(
    string JavaExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> ArgumentList,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    string? PreLaunchCommand = null,
    bool WaitForPreLaunchCommand = true,
    MinecraftGameProcessPriority ProcessPriority = MinecraftGameProcessPriority.Normal,
    int MainClassArgumentIndex = -1,
    bool PreferDedicatedGpu = false,
    MinecraftGameWindowMode WindowMode = MinecraftGameWindowMode.Default,
    int WindowWidth = MinecraftLaunchOptions.DefaultWindowWidth,
    int WindowHeight = MinecraftLaunchOptions.DefaultWindowHeight,
    string? WindowTitle = null)
{
    public MinecraftGameLaunchRequest WithLauncherWindowSize(int width, int height)
    {
        if (WindowMode != MinecraftGameWindowMode.Launcher ||
            !MinecraftLaunchOptions.IsValidWindowDimension(width) ||
            !MinecraftLaunchOptions.IsValidWindowDimension(height))
        {
            return this;
        }

        var arguments = ArgumentList.ToArray();
        ReplaceGameArgumentValue(arguments, "--width", width);
        ReplaceGameArgumentValue(arguments, "--height", height);
        return this with
        {
            ArgumentList = arguments,
            WindowWidth = width,
            WindowHeight = height,
        };
    }

    private void ReplaceGameArgumentValue(string[] arguments, string name, int value)
    {
        var start = Math.Clamp(MainClassArgumentIndex + 1, 0, arguments.Length);
        for (var index = start; index + 1 < arguments.Length; index++)
        {
            if (!string.Equals(arguments[index], name, StringComparison.Ordinal))
            {
                continue;
            }

            arguments[index + 1] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return;
        }
    }
}
