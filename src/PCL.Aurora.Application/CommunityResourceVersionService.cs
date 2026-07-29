using PCL.Aurora.Domain;

namespace PCL.Aurora.Application;

public sealed class CommunityResourceVersionService(
    ModrinthCommunityResourceVersionService modrinth,
    CurseForgeCommunityResourceVersionService curseForge) : ICommunityResourceVersionService
{
    public Task<CommunityResourceVersionCatalog> GetProjectVersionsAsync(
        string projectId,
        string? gameVersion,
        CommunityResourceLoader loader,
        CancellationToken cancellationToken = default) =>
        IsCurseForgeId(projectId)
            ? curseForge.GetProjectVersionsAsync(projectId, gameVersion, loader, cancellationToken)
            : modrinth.GetProjectVersionsAsync(projectId, gameVersion, loader, cancellationToken);

    public Task<CommunityResourceVersionCatalog> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default) =>
        IsCurseForgeId(versionId)
            ? curseForge.GetVersionAsync(versionId, cancellationToken)
            : modrinth.GetVersionAsync(versionId, cancellationToken);

    private static bool IsCurseForgeId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 20 && value.All(char.IsAsciiDigit);
}
