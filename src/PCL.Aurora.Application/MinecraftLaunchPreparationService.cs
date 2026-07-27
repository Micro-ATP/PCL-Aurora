using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

public sealed class MinecraftLaunchPreparationService(
    IMinecraftVersionPreparationService versionPreparationService,
    ILauncherPreferencesService? preferencesService = null,
    ISystemMemoryInfo? systemMemoryInfo = null)
    : IMinecraftLaunchPreparationService
{
    public async Task<MinecraftLaunchPreparation> PrepareAsync(
        MinecraftInstance instance,
        MinecraftAccount? account,
        JavaInstallation? java = null,
        CancellationToken cancellationToken = default)
    {
        var versionPreparation = await versionPreparationService
            .PrepareAsync(instance, cancellationToken)
            .ConfigureAwait(false);
        var metadata = versionPreparation.Inspection.EffectiveMetadata;
        if (metadata is null)
        {
            return new(
                versionPreparation,
                new MinecraftClasspathInspection([], [], ["未读取到有效版本元数据。"]),
                new MinecraftLaunchArgumentPreparation(null, ["未读取到有效版本元数据。"]));
        }

        var versionsDirectory = Directory.GetParent(instance.DirectoryPath)?.FullName;
        var minecraftRootDirectory = versionsDirectory is null
            ? null
            : Directory.GetParent(versionsDirectory)?.FullName;
        var launchOptions = preferencesService?.Current.EffectiveLaunchOptions ?? MinecraftLaunchOptions.Default;
        var javaRequirement = Pcl2MinecraftJavaRequirementEvaluator.Evaluate(metadata);
        var memoryAllocation = PrepareMemoryAllocation(
            launchOptions,
            systemMemoryInfo?.Get(),
            instance,
            minecraftRootDirectory,
            java);
        var context = MinecraftLaunchContext.CreateDefault(metadata.Id) with
        {
            NativesDirectory = Path.Combine(instance.DirectoryPath, "natives"),
            GameDirectory = minecraftRootDirectory,
            AssetsRoot = minecraftRootDirectory is null ? null : Path.Combine(minecraftRootDirectory, "assets"),
            AssetsIndexName = metadata.AssetIndex?.Id,
            VersionType = metadata.Type,
            Account = account,
            RuleEnvironment = versionPreparation.RuleEnvironment,
            ResolutionWidth = launchOptions.WindowMode == MinecraftGameWindowMode.Custom
                ? launchOptions.WindowWidth
                : MinecraftLaunchOptions.DefaultWindowWidth,
            ResolutionHeight = launchOptions.WindowMode == MinecraftGameWindowMode.Custom
                ? launchOptions.WindowHeight
                : MinecraftLaunchOptions.DefaultWindowHeight,
            MaximumMemoryMiB = memoryAllocation.Allocation?.MaximumMemoryMiB,
        };
        var classpathInspection = MinecraftClasspathBuilder.Build(
            versionPreparation.Inspection,
            minecraftRootDirectory,
            versionPreparation.RuleEnvironment);
        context = context with { Classpath = classpathInspection.Value };
        return new(
            versionPreparation,
            classpathInspection,
            MinecraftLaunchArgumentBuilder.Prepare(metadata, context, launchOptions),
            javaRequirement,
            memoryAllocation);
    }

    private static MinecraftMemoryAllocationPreparation PrepareMemoryAllocation(
        MinecraftLaunchOptions launchOptions,
        SystemMemoryInformation? memoryInformation,
        MinecraftInstance instance,
        string? minecraftRootDirectory,
        JavaInstallation? java)
    {
        if (launchOptions.MemoryAllocationMode == MinecraftMemoryAllocationMode.Custom)
        {
            return PclCeMinecraftMemoryAllocator.Prepare(
                launchOptions,
                memoryInformation?.TotalBytes ?? 0,
                memoryInformation?.AvailableBytes ?? 0,
                instance,
                modCount: 0,
                java);
        }

        if (memoryInformation is not { IsUsable: true } ||
            memoryInformation.TotalBytes is not { } totalBytes ||
            memoryInformation.AvailableBytes is not { } availableBytes)
        {
            return new(null, []);
        }

        var modCount = CountModFiles(minecraftRootDirectory);
        return PclCeMinecraftMemoryAllocator.Prepare(
            launchOptions,
            totalBytes,
            availableBytes,
            instance,
            modCount,
            java);
    }

    private static int CountModFiles(string? minecraftRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(minecraftRootDirectory))
        {
            return 0;
        }

        try
        {
            var modsDirectory = Path.Combine(minecraftRootDirectory, "mods");
            return Directory.Exists(modsDirectory)
                ? Directory.EnumerateFiles(modsDirectory).Take(10000).Count()
                : 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
