using PCL.Aurora.Application;
using PCL.Aurora.Domain;
using PCL.Aurora.Platform.Abstractions;

namespace PCL.Aurora.Application.Tests;

public sealed class MicrosoftAccountSessionServiceTests
{
    [Fact]
    public async Task PersistAndRestore_UsesSecureStoreAndRotatesRefreshToken()
    {
        var originalAccount = CreateAccount("minecraft-access-1");
        var refreshedAccount = CreateAccount("minecraft-access-2");
        var authentication = new RecordingAuthenticationService(new(refreshedAccount, "rotated-refresh-token"));
        var secrets = new MemorySecretStore();
        var service = new MicrosoftAccountSessionService(authentication, secrets);
        var original = new MicrosoftAuthenticationResult(originalAccount, "original-refresh-token");

        await service.PersistAsync(original);
        var restored = await service.RestoreAsync(MicrosoftAccountProfile.FromAuthenticatedAccount(originalAccount));

        Assert.Equal("original-refresh-token", authentication.ReceivedRefreshToken);
        Assert.Equal(refreshedAccount, restored.Account);
        Assert.Null(restored.Warning);
        Assert.Equal("rotated-refresh-token", secrets.Get("PCL Aurora Microsoft OAuth", originalAccount.Uuid));
    }

    [Fact]
    public async Task RestoreAsync_WhenKeychainHasNoToken_DoesNotCallAuthenticationService()
    {
        var authentication = new RecordingAuthenticationService(new(CreateAccount("unused"), "unused"));
        var service = new MicrosoftAccountSessionService(authentication, new MemorySecretStore());
        var profile = MicrosoftAccountProfile.FromAuthenticatedAccount(CreateAccount("minecraft-access"));

        var restored = await service.RestoreAsync(profile);

        Assert.Null(restored.Account);
        Assert.Contains("钥匙串", restored.Warning);
        Assert.Null(authentication.ReceivedRefreshToken);
    }

    private static MinecraftAccount CreateAccount(string accessToken) =>
        new("AuroraPlayer", "01234567-89ab-cdef-0123-456789abcdef", MinecraftAccountKind.Microsoft, true)
        {
            AccessToken = accessToken,
        };

    private sealed class RecordingAuthenticationService(MicrosoftAuthenticationResult refreshResult) : IMicrosoftAccountAuthenticationService
    {
        public string? ReceivedRefreshToken { get; private set; }

        public bool IsConfigured => true;

        public Task<MicrosoftDeviceCodeSession> BeginDeviceCodeLoginAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MicrosoftAuthenticationResult> CompleteDeviceCodeLoginAsync(MicrosoftDeviceCodeSession session, IProgress<MicrosoftAuthenticationProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MicrosoftAuthenticationResult> RefreshAsync(string refreshToken, IProgress<MicrosoftAuthenticationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            ReceivedRefreshToken = refreshToken;
            return Task.FromResult(refreshResult);
        }
    }

    private sealed class MemorySecretStore : ISecureSecretStore
    {
        private readonly Dictionary<(string Service, string Account), string> values = [];

        public Task<string?> GetAsync(string service, string account, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.TryGetValue((service, account), out var value) ? value : null);

        public Task SetAsync(string service, string account, string secret, CancellationToken cancellationToken = default)
        {
            values[(service, account)] = secret;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string service, string account, CancellationToken cancellationToken = default)
        {
            values.Remove((service, account));
            return Task.CompletedTask;
        }

        public string? Get(string service, string account) =>
            values.TryGetValue((service, account), out var value) ? value : null;
    }
}
