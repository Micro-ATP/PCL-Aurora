using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class LaunchReadinessTests
{
    [Fact]
    public void Evaluate_ReportsAllMissingLaunchRequirements()
    {
        var readiness = LaunchReadiness.Evaluate(null, null, null);

        Assert.False(readiness.CanLaunch);
        Assert.Equal(3, readiness.BlockingReasons.Count);
    }

    [Fact]
    public void Evaluate_AcceptsValidInstanceAccountAndJava()
    {
        var instance = new MinecraftInstance("1.21.4", "/versions/1.21.4", "1.21.4", "release", null, MinecraftInstanceStatus.Valid);
        var account = new MinecraftAccount("Alex", "00000000-0000-0000-0000-000000000000", MinecraftAccountKind.Offline, true);
        var java = new JavaInstallation("/java", "21", 21, "Test", JavaArchitecture.Arm64, JavaSource.Path, true);

        var readiness = LaunchReadiness.Evaluate(instance, account, java);

        Assert.True(readiness.CanLaunch);
        Assert.Empty(readiness.BlockingReasons);
    }
}
