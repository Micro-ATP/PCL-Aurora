using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftGameLaunchRequestBuilderTests
{
    [Fact]
    public void Prepare_PreservesIndividualArgumentsWithoutShellJoining()
    {
        var instance = new MinecraftInstance("1.21.4", "/minecraft/versions/1.21.4", "1.21.4", "release", null, MinecraftInstanceStatus.Valid);
        var java = new JavaInstallation("/usr/bin/java", "21", 21, "Test", JavaArchitecture.Arm64, JavaSource.Path, true);
        var arguments = new MinecraftLaunchArgumentPreparation(
            new MinecraftLaunchArguments(["-Dpath=/folder with spaces"], "example.Main", ["--name", "semi;colon"]),
            []);

        var preparation = MinecraftGameLaunchRequestBuilder.Prepare(instance, java, arguments);

        Assert.True(preparation.IsReady);
        Assert.Equal(["-Dpath=/folder with spaces", "example.Main", "--name", "semi;colon"], preparation.Request!.ArgumentList);
        Assert.Equal("/minecraft", preparation.Request.WorkingDirectory);
    }

    [Fact]
    public void Prepare_BlocksWhenArgumentPreparationIsIncomplete()
    {
        var instance = new MinecraftInstance("1.21.4", "/minecraft/versions/1.21.4", "1.21.4", "release", null, MinecraftInstanceStatus.Valid);
        var java = new JavaInstallation("/usr/bin/java", "21", 21, "Test", JavaArchitecture.Arm64, JavaSource.Path, true);

        var preparation = MinecraftGameLaunchRequestBuilder.Prepare(
            instance,
            java,
            new MinecraftLaunchArgumentPreparation(null, ["缺少 classpath。"]));

        Assert.False(preparation.IsReady);
        Assert.Contains("缺少 classpath。", preparation.BlockingReasons);
    }
}
