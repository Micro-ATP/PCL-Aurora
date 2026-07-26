namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftInstalledLoaderDetectorTests
{
    [Theory]
    [InlineData("net.fabricmc:fabric-loader:0.16.10", MinecraftLoaderKind.Fabric, "0.16.10", null)]
    [InlineData("net.minecraftforge:forge:1.20.1-47.2.0", MinecraftLoaderKind.Forge, "47.2.0", "1.20.1")]
    [InlineData("net.minecraftforge:fmlloader:1.21.4-54.1.0", MinecraftLoaderKind.Forge, "54.1.0", "1.21.4")]
    [InlineData("net.neoforged:neoforge:21.1.100", MinecraftLoaderKind.NeoForge, "21.1.100", null)]
    [InlineData("net.neoforged:forge:1.20.1-47.1.99", MinecraftLoaderKind.NeoForge, "47.1.99", "1.20.1")]
    public void Detect_RecognizesPcl2LoaderCoordinates(
        string coordinate,
        MinecraftLoaderKind kind,
        string version,
        string? minecraftVersion)
    {
        var result = MinecraftInstalledLoaderDetector.Detect([coordinate]);

        Assert.NotNull(result);
        Assert.Equal(kind, result!.Kind);
        Assert.Equal(version, result.Version);
        Assert.Equal(minecraftVersion, result.MinecraftVersion);
    }

    [Fact]
    public void Detect_PreservesPcl2FabricPriority()
    {
        var result = MinecraftInstalledLoaderDetector.Detect(
        [
            "net.minecraftforge:forge:1.20.1-47.2.0",
            "net.fabricmc:fabric-loader:0.16.10",
        ]);

        Assert.Equal(MinecraftLoaderKind.Fabric, result!.Kind);
    }
}
