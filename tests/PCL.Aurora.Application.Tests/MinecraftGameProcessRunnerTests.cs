using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Infrastructure;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftGameProcessRunnerTests
{
    [Fact]
    public async Task StartAsync_PreservesArgumentsAndCapturesOutput()
    {
        var request = new MinecraftGameLaunchRequest(
            "/usr/bin/printf",
            Path.GetTempPath(),
            ["%s|%s\\n", "value with spaces", "semi;colon"],
            new Dictionary<string, string>());
        var runner = new MinecraftGameProcessRunner();

        var session = await runner.StartAsync(request);
        var output = new List<GameProcessOutput>();
        await foreach (var outputLine in session.Output.ReadAllAsync())
        {
            output.Add(outputLine);
        }

        Assert.Equal(0, await session.ExitCode);
        var capturedLine = Assert.Single(output);
        Assert.False(capturedLine.IsError);
        Assert.Equal("value with spaces|semi;colon", capturedLine.Text);
    }
}
