using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class CommunityResourceDependencyResolverTests
{
    [Fact]
    public async Task ResolveAsync_SeparatesRequiredAndOptionalDependencies()
    {
        var required = CreateVersion("required-version", "required-project", []);
        var optionalRequired = CreateVersion("optional-required-version", "optional-required-project", []);
        var optional = CreateVersion(
            "optional-version",
            "optional-project",
            [new("optional-required-project", null, null, CommunityResourceDependencyType.Required)]);
        var root = CreateVersion(
            "root-version",
            "root-project",
            [
                new("required-project", null, null, CommunityResourceDependencyType.Required),
                new("optional-project", null, null, CommunityResourceDependencyType.Optional),
            ]);
        var service = new CommunityResourceDependencyResolver(new StubVersionService(
            [required, optional, optionalRequired]));

        var result = await service.ResolveAsync(
            root,
            "1.21.1",
            CommunityResourceLoader.Fabric);

        Assert.Equal("required-version", Assert.Single(result.RequiredVersions).Id);
        var optionalBundle = Assert.Single(result.OptionalDependencies);
        Assert.Equal("optional-version", optionalBundle.Id);
        Assert.Equal(["optional-version", "optional-required-version"], optionalBundle.Versions.Select(item => item.Id));
        Assert.Empty(result.Errors);
    }

    private static CommunityResourceVersion CreateVersion(
        string id,
        string projectId,
        IReadOnlyList<CommunityResourceDependency> dependencies) =>
        new(
            id,
            projectId,
            id,
            "1.0.0",
            CommunityResourceVersionChannel.Release,
            DateTimeOffset.UtcNow,
            0,
            ["1.21.1"],
            ["fabric"],
            [new($"{id}.jar", new Uri($"https://cdn.modrinth.com/data/{projectId}/versions/{id}/{id}.jar"), new string('a', 40), 1, true)],
            dependencies);

    private sealed class StubVersionService(IReadOnlyList<CommunityResourceVersion> versions)
        : ICommunityResourceVersionService
    {
        public Task<CommunityResourceVersionCatalog> GetProjectVersionsAsync(
            string projectId,
            string? gameVersion,
            CommunityResourceLoader loader,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommunityResourceVersionCatalog(
                versions.Where(version => version.ProjectId == projectId).ToArray(),
                []));

        public Task<CommunityResourceVersionCatalog> GetVersionAsync(
            string versionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CommunityResourceVersionCatalog(
                versions.Where(version => version.Id == versionId).ToArray(),
                []));
    }
}
