using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftLoaderInstallerPlanBuilderTests
{
    [Fact]
    public void Build_Forge_UsesVerifiedMirrorFirstAndSeparateArguments()
    {
        var loader = new MinecraftLoaderCatalogEntry(
            MinecraftLoaderKind.Forge,
            "1.20.1",
            "47.2.0",
            MinecraftLoaderChannel.Release,
            false,
            new PclCeForgeVersionEntry("47.2.0", null, "1.20.1"));
        var java = new JavaInstallation("/usr/bin/java", "21", 21, "Test", JavaArchitecture.Arm64, JavaSource.Path, true);

        var plan = MinecraftLoaderInstallerPlanBuilder.Build(loader, "/tmp/pcl-aurora-loader", java);

        Assert.True(plan.CanInstall);
        Assert.Equal("bmclapi2.bangbang93.com", plan.InstallerArtifact!.Url.Host);
        Assert.Equal("maven.minecraftforge.net", Assert.Single(plan.InstallerArtifact.AlternativeUrls!).Host);
        Assert.Equal("maven.minecraftforge.net", plan.InstallerArtifact.Sha1Url!.Host);
        Assert.Equal(["-jar", Path.Combine("/tmp/pcl-aurora-loader", plan.InstallerArtifact.RelativePath), "--installClient", "/tmp/pcl-aurora-loader"], plan.ProcessRequest!.ArgumentList);
    }

    [Fact]
    public void Build_NeoForge_RemovesOfficialReleasesSegmentForPclCeMirrorPath()
    {
        var neoForge = new PclCeNeoForgeListEntry("20.1.1");
        var loader = new MinecraftLoaderCatalogEntry(
            MinecraftLoaderKind.NeoForge,
            "1.20.1",
            "20.1.1",
            MinecraftLoaderChannel.Release,
            false,
            neoForge);
        var java = new JavaInstallation("/usr/bin/java", "21", 21, "Test", JavaArchitecture.Arm64, JavaSource.Path, true);

        var plan = MinecraftLoaderInstallerPlanBuilder.Build(loader, "/tmp/pcl-aurora-loader", java);

        Assert.True(plan.CanInstall);
        Assert.StartsWith("/maven/net/neoforged/neoforge/", plan.InstallerArtifact!.Url.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("maven.neoforged.net", Assert.Single(plan.InstallerArtifact.AlternativeUrls!).Host);
    }

    [Fact]
    public void Build_OptiFine_UsesPclCePublicDownloadPathAndInstallerMainClass()
    {
        var loader = new MinecraftLoaderCatalogEntry(
            MinecraftLoaderKind.OptiFine,
            "1.20.1",
            "I6",
            MinecraftLoaderChannel.Release,
            true,
            null,
            new("OptiFine_1.20.1_HD_U_I6.jar", "HD_U", "I6", false, "47.2.0"));
        var java = new JavaInstallation("/usr/bin/java", "21", 21, "Test", JavaArchitecture.Arm64, JavaSource.Path, true);

        var plan = MinecraftLoaderInstallerPlanBuilder.Build(loader, "/tmp/pcl-aurora-loader", java);

        Assert.True(plan.CanInstall);
        Assert.Equal("bmclapi2.bangbang93.com", plan.InstallerArtifact!.Url.Host);
        Assert.Equal("/optifine/1.20.1/HD_U/I6", plan.InstallerArtifact.Url.AbsolutePath);
        Assert.Null(plan.InstallerArtifact.Sha1Url);
        Assert.Equal(300 * 1024, plan.InstallerArtifact.MinimumSize);
        Assert.Equal(["-cp", Path.Combine("/tmp/pcl-aurora-loader", plan.InstallerArtifact.RelativePath), "optifine.Installer"], plan.ProcessRequest!.ArgumentList);
    }

    [Fact]
    public void Build_OptiFinePreview_UsesVerifiedPublicCatalogPath()
    {
        var loader = new MinecraftLoaderCatalogEntry(
            MinecraftLoaderKind.OptiFine,
            "1.20.1",
            "I6 pre6",
            MinecraftLoaderChannel.Beta,
            false,
            null,
            new("preview_OptiFine_1.20.1_HD_U_I6_pre6.jar", "HD_U_I6", "pre6", true, "47.1.43"));
        var java = new JavaInstallation("/usr/bin/java", "21", 21, "Test", JavaArchitecture.Arm64, JavaSource.Path, true);

        var plan = MinecraftLoaderInstallerPlanBuilder.Build(loader, "/tmp/pcl-aurora-loader", java);

        Assert.True(plan.CanInstall);
        Assert.Equal("/optifine/1.20.1/HD_U_I6/pre6", plan.InstallerArtifact!.Url.AbsolutePath);
    }

    [Fact]
    public void Build_LegacyOptiFine_StopsBeforeDownloadingOrRunningTheUnmigratedJsonPath()
    {
        var loader = new MinecraftLoaderCatalogEntry(
            MinecraftLoaderKind.OptiFine,
            "1.12.2",
            "C9",
            MinecraftLoaderChannel.Release,
            true,
            null,
            new("OptiFine_1.12.2_HD_U_C9.jar", "HD_U", "C9", false, null));
        var java = new JavaInstallation("/usr/bin/java", "8", 8, "Test", JavaArchitecture.Arm64, JavaSource.Path, true);

        var plan = MinecraftLoaderInstallerPlanBuilder.Build(loader, "/tmp/pcl-aurora-loader", java);

        Assert.False(plan.CanInstall);
        Assert.Null(plan.InstallerArtifact);
        Assert.Contains(plan.BlockingReasons, reason => reason.Contains("旧版继承 JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void ParseLatestStableInstallerUri_RejectsNonOfficialUrl()
    {
        const string json = """[{ "version": "1.0.1", "stable": true, "url": "https://example.invalid/fabric-installer.jar" }]""";

        Assert.Null(MinecraftFabricInstallerMetadataParser.ParseLatestStableInstallerUri(json));
    }
}
