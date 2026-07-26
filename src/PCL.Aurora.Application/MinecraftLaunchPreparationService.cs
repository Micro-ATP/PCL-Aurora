using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class MinecraftLaunchPreparationService(IMinecraftVersionPreparationService versionPreparationService)
    : IMinecraftLaunchPreparationService
{
    public async Task<MinecraftLaunchPreparation> PrepareAsync(
        MinecraftInstance instance,
        MinecraftAccount? account,
        CancellationToken cancellationToken = default)
    {
        var versionPreparation = await versionPreparationService
            .PrepareAsync(instance, cancellationToken)
            .ConfigureAwait(false);
        var metadata = versionPreparation.Inspection.EffectiveMetadata;
        if (metadata is null)
        {
            return new(versionPreparation, new MinecraftLaunchArgumentPreparation(null, ["未读取到有效版本元数据。"]));
        }

        var versionsDirectory = Directory.GetParent(instance.DirectoryPath)?.FullName;
        var minecraftRootDirectory = versionsDirectory is null
            ? null
            : Directory.GetParent(versionsDirectory)?.FullName;
        var context = MinecraftLaunchContext.CreateDefault(metadata.Id) with
        {
            NativesDirectory = Path.Combine(instance.DirectoryPath, "natives"),
            GameDirectory = minecraftRootDirectory,
            AssetsRoot = minecraftRootDirectory is null ? null : Path.Combine(minecraftRootDirectory, "assets"),
            AssetsIndexName = metadata.AssetIndex?.Id,
            VersionType = metadata.Type,
            Account = account,
        };
        return new(versionPreparation, MinecraftLaunchArgumentBuilder.Prepare(metadata, context));
    }
}
