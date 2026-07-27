using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class PclCeMinecraftMemoryAllocatorTests
{
    private const long GiB = 1024L * 1024L * 1024L;

    [Fact]
    public void Prepare_AutomaticUsesPclCeStagesForVanillaInstance()
    {
        var instance = new MinecraftInstance("1.21.4", "/minecraft/versions/1.21.4", "1.21.4", "release", null, MinecraftInstanceStatus.Valid);

        var result = PclCeMinecraftMemoryAllocator.Prepare(
            MinecraftLaunchOptions.Default,
            totalMemoryBytes: 8 * GiB,
            availableMemoryBytes: 4 * GiB,
            instance,
            modCount: 0,
            java: null);

        Assert.True(result.IsReady);
        Assert.True(result.Allocation!.IsAutomatic);
        Assert.Equal(2969, result.Allocation.MaximumMemoryMiB);
    }

    [Fact]
    public void Prepare_CustomUsesExplicitMiBValue()
    {
        var options = new MinecraftLaunchOptions(MemoryAllocationMode: MinecraftMemoryAllocationMode.Custom, CustomMemoryMiB: 6144);

        var result = PclCeMinecraftMemoryAllocator.Prepare(
            options,
            totalMemoryBytes: 16 * GiB,
            availableMemoryBytes: 8 * GiB,
            instance: null,
            modCount: 0,
            java: null);

        Assert.True(result.IsReady);
        Assert.False(result.Allocation!.IsAutomatic);
        Assert.Equal(6144, result.Allocation.MaximumMemoryMiB);
    }

    [Fact]
    public void Prepare_CustomDoesNotRequireSystemMemoryFacts()
    {
        var options = new MinecraftLaunchOptions(
            MemoryAllocationMode: MinecraftMemoryAllocationMode.Custom,
            CustomMemoryMiB: 3072);

        var result = PclCeMinecraftMemoryAllocator.Prepare(
            options,
            totalMemoryBytes: 0,
            availableMemoryBytes: 0,
            instance: null,
            modCount: 0,
            java: null);

        Assert.True(result.IsReady);
        Assert.Equal(3072, result.Allocation!.MaximumMemoryMiB);
    }

    [Fact]
    public void Prepare_RejectsMissingMemoryFacts()
    {
        var result = PclCeMinecraftMemoryAllocator.Prepare(
            MinecraftLaunchOptions.Default,
            totalMemoryBytes: 0,
            availableMemoryBytes: 0,
            instance: null,
            modCount: 0,
            java: null);

        Assert.False(result.IsReady);
        Assert.NotEmpty(result.BlockingReasons);
    }
}
