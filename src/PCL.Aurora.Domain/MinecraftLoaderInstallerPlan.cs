namespace PCL.Aurora.Domain;

public sealed record MinecraftLoaderInstallerPlan(
    MinecraftLoaderCatalogEntry Loader,
    MinecraftDownloadArtifact? InstallerArtifact,
    MinecraftLoaderInstallerProcessRequest? ProcessRequest,
    IReadOnlyList<string> BlockingReasons)
{
    public bool CanInstall => InstallerArtifact is not null && ProcessRequest is not null && BlockingReasons.Count == 0;
}
