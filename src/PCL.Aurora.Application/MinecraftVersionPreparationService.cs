using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application;

public sealed class MinecraftVersionPreparationService(
    IMinecraftVersionMetadataReader metadataReader,
    IPlatformInfo platformInfo)
    : IMinecraftVersionPreparationService
{
    public async Task<MinecraftVersionPreparation> PrepareAsync(
        MinecraftInstance instance,
        CancellationToken cancellationToken = default)
    {
        var inspection = await metadataReader.InspectAsync(instance, cancellationToken).ConfigureAwait(false);
        var platform = platformInfo.Get();
        var ruleEnvironment = CreateRuleEnvironment(platform);
        return new(
            inspection,
            MinecraftDownloadPlanBuilder.Create(inspection, platform.Architecture, ruleEnvironment),
            ruleEnvironment);
    }

    private static MinecraftLaunchRuleEnvironment CreateRuleEnvironment(PlatformInformation platform) => new(
        platform.OperatingSystem switch
        {
            "macOS" => "osx",
            "Windows" => "windows",
            "Linux" => "linux",
            _ => "unknown",
        },
        platform.Version,
        platform.Architecture switch
        {
            JavaArchitecture.X64 => "x86_64",
            JavaArchitecture.Arm64 => "arm64",
            _ => null,
        });
}
