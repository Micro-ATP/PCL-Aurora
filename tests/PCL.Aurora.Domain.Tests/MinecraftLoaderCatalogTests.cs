using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftLoaderCatalogTests
{
    [Fact]
    public void Parse_ReadsForgeNeoForgeAndFabricEntries()
    {
        var result = MinecraftLoaderCatalogParser.Parse(
            """
            {
              "source": "本地测试目录",
              "loaders": [
                { "kind": "forge", "minecraftVersion": "1.20.1", "version": "47.2.0", "recommended": true },
                { "kind": "neoforge", "minecraftVersion": "1.20.1", "version": "1.20.1-47.1.99" },
                { "kind": "fabric", "minecraftVersion": "1.20.1", "version": "0.16.10" }
              ]
            }
            """);

        Assert.True(result.IsSuccess);
        Assert.Equal("本地测试目录", result.Catalog!.SourceName);
        Assert.Equal(3, result.Catalog.Entries.Count);
        Assert.IsType<PclCeForgeVersionEntry>(result.Catalog.Entries[0].ForgelikeEntry);
        Assert.IsType<PclCeNeoForgeListEntry>(result.Catalog.Entries[1].ForgelikeEntry);
        Assert.Null(result.Catalog.Entries[2].ForgelikeEntry);
    }

    [Fact]
    public void Parse_RejectsDuplicateAndMismatchedNeoForgeEntries()
    {
        var result = MinecraftLoaderCatalogParser.Parse(
            """
            {
              "loaders": [
                { "kind": "forge", "minecraftVersion": "1.20.1", "version": "47.2.0" },
                { "kind": "forge", "minecraftVersion": "1.20.1", "version": "47.2.0" },
                { "kind": "neoforge", "minecraftVersion": "1.20.1", "version": "20.6.119-beta" }
              ]
            }
            """);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("重复版本", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("不一致", StringComparison.Ordinal));
    }

    [Fact]
    public void Filter_UsesPclCeVersionOrderingAndSelectionRemainsExclusive()
    {
        var result = MinecraftLoaderCatalogParser.Parse(
            """
            {
              "loaders": [
                { "kind": "forge", "minecraftVersion": "1.20.1", "version": "47.1.0" },
                { "kind": "forge", "minecraftVersion": "1.20.1", "version": "47.2.0" },
                { "kind": "fabric", "minecraftVersion": "1.20.1", "version": "0.16.10" }
              ]
            }
            """);

        var entries = MinecraftLoaderCatalogFilter.ForMinecraftVersion(result.Catalog!, "1.20.1", MinecraftLoaderKind.Forge);
        Assert.Equal("47.2.0", entries[0].Version);

        var compatibility = MinecraftLoaderCompatibilityEvaluator.Evaluate("1.20.1", [entries[0], result.Catalog!.Entries.Single(entry => entry.Kind == MinecraftLoaderKind.Fabric)]);
        Assert.False(compatibility.IsCompatible);
        Assert.Contains(compatibility.Reasons, reason => reason.Contains("一次只能选择", StringComparison.Ordinal));
    }
}
