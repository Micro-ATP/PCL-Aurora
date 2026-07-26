namespace PCL.Aurora.Domain;

public static class MinecraftGameLaunchRequestBuilder
{
    public static MinecraftGameLaunchRequestPreparation Prepare(
        MinecraftInstance? instance,
        JavaInstallation? java,
        MinecraftLaunchArgumentPreparation argumentPreparation)
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

        var argumentList = new List<string>(argumentPreparation.Arguments.JvmArguments.Count + argumentPreparation.Arguments.GameArguments.Count + 1);
        argumentList.AddRange(argumentPreparation.Arguments.JvmArguments);
        argumentList.Add(argumentPreparation.Arguments.MainClass);
        argumentList.AddRange(argumentPreparation.Arguments.GameArguments);
        return new(
            new MinecraftGameLaunchRequest(
                java.ExecutablePath,
                minecraftRootDirectory,
                argumentList,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["PCL_AURORA_MINECRAFT_DIRECTORY"] = minecraftRootDirectory,
                }),
            []);
    }
}
