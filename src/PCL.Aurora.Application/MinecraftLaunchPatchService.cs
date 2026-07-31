using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

/// <summary>
/// Directly adapts the launch-patch selection and extraction flow from PCL-CE
/// ModLaunch.cs. Resources are materialized only when the selected version needs them.
/// </summary>
public sealed class MinecraftLaunchPatchService(IPlatformPaths platformPaths) : IMinecraftLaunchPatchService
{
    private static readonly DateTimeOffset LegacyFixCutoff = new(2013, 6, 25, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LegacyMergeSortCutoff = new(2011, 5, 25, 0, 0, 0, TimeSpan.Zero);
    private static readonly SemaphoreSlim ExtractionLock = new(1, 1);

    public async Task<MinecraftLaunchPatchPreparation> PrepareAsync(
        MinecraftInstance instance,
        MinecraftVersionMetadata metadata,
        JavaInstallation java,
        MinecraftLaunchOptions options,
        MinecraftGameLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(java);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);

        if (request.MainClassArgumentIndex < 0 || request.MainClassArgumentIndex >= request.ArgumentList.Count)
        {
            return new(null, ["无法定位 Minecraft 主类，不能安全应用启动补丁。"]);
        }

        var patchDirectory = Path.Combine(platformPaths.Get().CacheDirectory, "LaunchPatches");
        var prefixArguments = new List<string>();
        try
        {
            if (NeedsLegacyFix(instance, metadata, options))
            {
                var legacyFixPath = await ExtractAsync(
                    patchDirectory,
                    "legacyfix.jar",
                    "PCL.Aurora.Application.Assets.LaunchPatches.legacyfix.jar",
                    cancellationToken).ConfigureAwait(false);
                prefixArguments.Add($"-javaagent:{legacyFixPath}");
                if ((metadata.ReleaseTime ?? instance.ReleaseTime) < LegacyMergeSortCutoff)
                {
                    prefixArguments.Add("-Djava.util.Arrays.useLegacyMergeSort=true");
                }
            }

            if (NeedsLwjglUnsafeAgent(metadata, options))
            {
                var unsafeAgentPath = await ExtractAsync(
                    patchDirectory,
                    "lwjgl-unsafe-agent.jar",
                    "PCL.Aurora.Application.Assets.LaunchPatches.lwjgl-unsafe-agent.jar",
                    cancellationToken).ConfigureAwait(false);
                prefixArguments.Add($"-javaagent:{unsafeAgentPath}");
            }

            if (NeedsJavaWrapper(java, options, request.WorkingDirectory))
            {
                var wrapperPath = await ExtractAsync(
                    patchDirectory,
                    "java-wrapper.jar",
                    "PCL.Aurora.Application.Assets.LaunchPatches.java-wrapper.jar",
                    cancellationToken).ConfigureAwait(false);
                if (java.MajorVersion >= 9)
                {
                    prefixArguments.Add("--add-exports");
                    prefixArguments.Add("cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
                }
                prefixArguments.Add($"-Doolloo.jlw.tmpdir={patchDirectory}");
                prefixArguments.Add("-jar");
                prefixArguments.Add(wrapperPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new(null, [$"准备启动补丁失败：{exception.Message}"]);
        }

        if (prefixArguments.Count == 0)
        {
            return new(request, []);
        }

        var arguments = request.ArgumentList.ToList();
        arguments.InsertRange(request.MainClassArgumentIndex, prefixArguments);
        return new(
            request with
            {
                ArgumentList = arguments,
                MainClassArgumentIndex = request.MainClassArgumentIndex + prefixArguments.Count,
            },
            []);
    }

    private static bool NeedsLegacyFix(
        MinecraftInstance instance,
        MinecraftVersionMetadata metadata,
        MinecraftLaunchOptions options)
    {
        var releaseTime = metadata.ReleaseTime ?? instance.ReleaseTime;
        return !options.DisableLegacyFix &&
               releaseTime is { Year: > 2000 } &&
               releaseTime < LegacyFixCutoff;
    }

    private static bool NeedsLwjglUnsafeAgent(MinecraftVersionMetadata metadata, MinecraftLaunchOptions options) =>
        !options.DisableLwjglUnsafeAgent &&
        (metadata.Libraries ?? []).Any(library =>
            string.Equals(library.Name, "org.lwjgl:lwjgl:3.4.1", StringComparison.OrdinalIgnoreCase));

    private static bool NeedsJavaWrapper(
        JavaInstallation java,
        MinecraftLaunchOptions options,
        string workingDirectory) =>
        OperatingSystem.IsWindows() &&
        !options.DisableJavaLaunchWrapper &&
        java.Architecture != JavaArchitecture.Arm64 &&
        java.MajorVersion <= 8 &&
        workingDirectory.Any(character => character > 127);

    private static async Task<string> ExtractAsync(
        string directory,
        string fileName,
        string resourceName,
        CancellationToken cancellationToken)
    {
        await ExtractionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(directory);
            var destinationPath = Path.Combine(directory, fileName);
            var temporaryPath = destinationPath + ".partial";
            await using var resource = typeof(MinecraftLaunchPatchService).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidDataException($"内置补丁资源缺失：{fileName}");
            try
            {
                await using (var destination = new FileStream(
                                 temporaryPath,
                                 FileMode.Create,
                                 FileAccess.Write,
                                 FileShare.None,
                                 81920,
                                 FileOptions.Asynchronous))
                {
                    await resource.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (IOException) { }
            }
            return destinationPath;
        }
        finally
        {
            ExtractionLock.Release();
        }
    }
}
