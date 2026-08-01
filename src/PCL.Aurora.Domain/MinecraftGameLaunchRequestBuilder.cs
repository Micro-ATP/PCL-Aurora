namespace PCL.Aurora.Domain;

public static class MinecraftGameLaunchRequestBuilder
{
    public static MinecraftGameLaunchRequestPreparation Prepare(
        MinecraftInstance? instance,
        JavaInstallation? java,
        MinecraftLaunchArgumentPreparation argumentPreparation,
        MinecraftLaunchOptions? launchOptions = null)
    {
        ArgumentNullException.ThrowIfNull(argumentPreparation);
        var blockingReasons = new List<string>();
        if (instance is null || instance.Status != MinecraftInstanceStatus.Valid)
        {
            blockingReasons.Add("未选择有效的 Minecraft 实例。");
        }

        if (java is null || !java.IsCompatible || string.IsNullOrWhiteSpace(java.ExecutablePath))
        {
            blockingReasons.Add("未找到兼容的 Java 可执行文件。");
        }

        if (!argumentPreparation.IsReady || argumentPreparation.Arguments is null)
        {
            blockingReasons.AddRange(argumentPreparation.BlockingReasons);
        }

        var versionsDirectory = instance is null ? null : Directory.GetParent(instance.DirectoryPath)?.FullName;
        var minecraftRootDirectory = versionsDirectory is null ? null : Directory.GetParent(versionsDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(minecraftRootDirectory))
        {
            blockingReasons.Add("无法确定 Minecraft 工作目录。");
        }

        if (blockingReasons.Count > 0 || instance is null || java is null || argumentPreparation.Arguments is null || minecraftRootDirectory is null)
        {
            return new(null, blockingReasons.Distinct(StringComparer.Ordinal).ToList());
        }

        launchOptions ??= MinecraftLaunchOptions.Default;
        var gameDirectory = MinecraftInstanceIsolationResolver.ResolveGameDirectory(
            instance,
            minecraftRootDirectory,
            launchOptions.InstanceIsolationMode);
        var argumentList = new List<string>(argumentPreparation.Arguments.JvmArguments.Count + argumentPreparation.Arguments.GameArguments.Count + 1);
        argumentList.AddRange(argumentPreparation.Arguments.JvmArguments);
        argumentList.Add(argumentPreparation.Arguments.MainClass);
        argumentList.AddRange(argumentPreparation.Arguments.GameArguments);
        var environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PCL_AURORA_MINECRAFT_DIRECTORY"] = minecraftRootDirectory,
        };
        if (OperatingSystem.IsLinux() && launchOptions.PreferDedicatedGpu)
        {
            environmentVariables["DRI_PRIME"] = "1";
            environmentVariables["__NV_PRIME_RENDER_OFFLOAD"] = "1";
            environmentVariables["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia";
        }
        if (OperatingSystem.IsLinux())
        {
            switch (launchOptions.Renderer)
            {
                case MinecraftRendererMode.Software:
                    environmentVariables["LIBGL_ALWAYS_SOFTWARE"] = "1";
                    environmentVariables["GALLIUM_DRIVER"] = "llvmpipe";
                    break;
                case MinecraftRendererMode.DirectX12:
                    environmentVariables["MESA_LOADER_DRIVER_OVERRIDE"] = "d3d12";
                    break;
                case MinecraftRendererMode.Vulkan:
                    environmentVariables["MESA_LOADER_DRIVER_OVERRIDE"] = "zink";
                    break;
            }
        }

        return new(
            new MinecraftGameLaunchRequest(
                MinecraftJavaExecutableResolver.Resolve(
                    java.ExecutablePath,
                    launchOptions.UseJavaExecutable,
                    OperatingSystem.IsWindows()),
                gameDirectory,
                argumentList,
                environmentVariables,
                launchOptions.PreLaunchCommand,
                launchOptions.WaitForPreLaunchCommand,
                launchOptions.ProcessPriority,
                argumentPreparation.Arguments.JvmArguments.Count,
                launchOptions.PreferDedicatedGpu,
                launchOptions.WindowMode,
                launchOptions.WindowWidth,
                launchOptions.WindowHeight,
                string.IsNullOrWhiteSpace(launchOptions.WindowTitle)
                    ? null
                    : launchOptions.WindowTitle.Trim()),
            []);
    }
}
