namespace PCL.Aurora.Domain;

public sealed record MinecraftLoaderInstallerPlan(
    MinecraftLoaderCatalogEntry Loader,
    MinecraftDownloadArtifact? InstallerArtifact,
    MinecraftLoaderInstallerProcessRequest? ProcessRequest,
    IReadOnlyList<string> BlockingReasons,
    MinecraftLegacyOptiFineInstallation? LegacyOptiFineInstallation = null)
{
    public bool CanInstall => InstallerArtifact is not null &&
                              (ProcessRequest is not null || LegacyOptiFineInstallation is not null) &&
                              BlockingReasons.Count == 0;
}
