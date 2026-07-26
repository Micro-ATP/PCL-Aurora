using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Infrastructure;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftLoaderInstallerProcessRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_PreservesArgumentsAndCapturesOutput()
    {
        var request = new MinecraftLoaderInstallerProcessRequest(
            "/usr/bin/printf",
            Path.GetTempPath(),
            ["%s|%s\\n", "value with spaces", "semi;colon"]);
        var runner = new MinecraftLoaderInstallerProcessRunner();

        var result = await runner.ExecuteAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        var output = Assert.Single(result.Output);
        Assert.False(output.IsError);
        Assert.Equal("value with spaces|semi;colon", output.Text);
    }
}
