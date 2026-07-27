using PCL.Aurora.Platform.MacOS;

namespace PCL.Aurora.Platform.Tests;

public sealed class MacOSSystemMemoryInfoTests
{
    [Fact]
    public void Get_ReturnsUsableNonNegativeMacOSMemoryFacts()
    {
        var memory = new MacOSSystemMemoryInfo().Get();

        Assert.True(memory.IsUsable);
        Assert.True(memory.TotalBytes > 0);
        Assert.True(memory.AvailableBytes > 0);
        Assert.True(memory.AvailableBytes <= memory.TotalBytes);
    }
}
