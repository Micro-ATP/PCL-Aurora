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
            """[{ "loader": { "version": "0.16.10", "stable": true } }, { "loader": { "version": "0.16.9", "stable": false } }]""");

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Catalog!.Entries, entry => entry.Kind == MinecraftLoaderKind.Forge && entry.Version == "47.2.0");
        Assert.Contains(result.Catalog.Entries, entry => entry.Kind == MinecraftLoaderKind.NeoForge && entry.Version == "20.1.1");
        Assert.Contains(result.Catalog.Entries, entry => entry.Kind == MinecraftLoaderKind.NeoForge && entry.Version == "1.20.1-47.1.99");
        Assert.Contains(result.Catalog.Entries, entry => entry.Kind == MinecraftLoaderKind.Fabric && entry.Version == "0.16.10" && entry.IsRecommended);
    }

    [Fact]
    public void Parse_RejectsResponsesWithoutCompatibleEntries()
    {
        var result = MinecraftOfficialLoaderCatalogParser.Parse(
            "1.20.1",
            """<metadata><versioning><versions><version>1.19.4-45.2.1</version></versions></versioning></metadata>""",
            """{ "versions": [] }""",
            """{ "versions": [] }""",
            """[]""");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("没有兼容", StringComparison.Ordinal));
    }
}
