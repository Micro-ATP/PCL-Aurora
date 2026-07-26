using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Application.Tests;

public sealed class MinecraftGameLaunchServiceTests
{
    [Fact]
    public async Task LaunchAsync_WhenPreparationIsBlocked_DoesNotCallProcessRunner()
    {
        var processRunner = new TrackingProcessRunner();
        var service = new MinecraftGameLaunchService(
            new LaunchReadinessService(),
            new UnusedLaunchPreparationService(),
            processRunner);

        var preparation = await service.PrepareAsync(instance: null, account: null, java: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchAsync(preparation));
        Assert.False(processRunner.WasCalled);
    }

    private sealed class UnusedLaunchPreparationService : IMinecraftLaunchPreparationService
    {
        public Task<MinecraftLaunchPreparation> PrepareAsync(
            MinecraftInstance instance,
            MinecraftAccount? account,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("无效实例不应请求启动参数。");
    }

    private sealed class TrackingProcessRunner : IGameProcessRunner
    {
        public bool WasCalled { get; private set; }

        public Task<GameProcessSession> StartAsync(
            MinecraftGameLaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("阻断状态不应启动进程。");
        }
    }
}
