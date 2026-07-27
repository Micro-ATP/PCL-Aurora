using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftOfficialLoaderCatalogParserTests
{
    [Fact]
    public void Parse_MergesOfficialForgeNeoForgeAndFabricEntriesForRequestedMinecraftVersion()
    {
        var result = MinecraftOfficialLoaderCatalogParser.Parse(
            "1.20.1",
            """<metadata><versioning><versions><version>1.20.1-47.2.0</version><version>1.19.4-45.2.1</version></versions></versioning></metadata>""",
            """{ "versions": ["20.1.1", "20.1.1-beta"] }""",
            """{ "versions": ["1.20.1-47.1.99"] }""",
            """[{ "loader": { "version": "0.16.10", "stable": true } }, { "loader": { "version": "0.16.9", "stable": false } }]""",
            """[{ "mcversion": "1.20.1", "type": "HD_U", "patch": "I6", "filename": "OptiFine_1.20.1_HD_U_I6.jar", "forge": "Forge 47.2.0" }]""");

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Catalog!.Entries, entry => entry.Kind == MinecraftLoaderKind.Forge && entry.Version == "47.2.0");
        Assert.Contains(result.Catalog.Entries, entry => entry.Kind == MinecraftLoaderKind.NeoForge && entry.Version == "20.1.1");
        Assert.Contains(result.Catalog.Entries, entry => entry.Kind == MinecraftLoaderKind.NeoForge && entry.Version == "1.20.1-47.1.99");
        Assert.Contains(result.Catalog.Entries, entry => entry.Kind == MinecraftLoaderKind.Fabric && entry.Version == "0.16.10" && entry.IsRecommended);
        var optiFine = Assert.Single(result.Catalog.Entries, entry => entry.Kind == MinecraftLoaderKind.OptiFine);
        Assert.Equal("I6", optiFine.Version);
        Assert.Equal("HD_U/I6", optiFine.OptiFineEntry!.DownloadPath);
    }

    [Fact]
    public void Parse_NormalizesPreviewDownloadPathFromThePublicCatalog()
    {
        var result = MinecraftOfficialLoaderCatalogParser.Parse(
            "1.20.1",
            null,
            null,
            null,
            null,
            """[{ "mcversion": "1.20.1", "type": "HD_U_I6", "patch": "pre6", "filename": "preview_OptiFine_1.20.1_HD_U_I6_pre6.jar", "forge": "Forge 47.1.43" }]""");

        var optiFine = Assert.Single(result.Catalog!.Entries);

        Assert.Equal("I6 pre6", optiFine.Version);
        Assert.True(optiFine.IsPrerelease);
        Assert.Equal("HD_U_I6/pre6", optiFine.OptiFineEntry!.DownloadPath);
    }

    [Fact]
    public void Parse_RejectsResponsesWithoutCompatibleEntries()
    {
        var result = MinecraftOfficialLoaderCatalogParser.Parse(
            "1.20.1",
            """<metadata><versioning><versions><version>1.19.4-45.2.1</version></versions></versioning></metadata>""",
            """{ "versions": [] }""",
            """{ "versions": [] }""",
            """[]""",
            """[]""");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("没有兼容", StringComparison.Ordinal));
    }
}
