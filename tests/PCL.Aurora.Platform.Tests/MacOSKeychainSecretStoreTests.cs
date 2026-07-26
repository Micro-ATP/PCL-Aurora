using PCL.Aurora.Platform.MacOS;

namespace PCL.Aurora.Platform.Tests;

public sealed class MacOSKeychainSecretStoreTests
{
    [Fact]
    public async Task SetGetAndDeleteAsync_UsesLoginKeychainWithoutWritingAFile()
    {
        var service = $"PCL Aurora Test {Guid.NewGuid():N}";
        const string account = "keychain-roundtrip";
        var store = new MacOSKeychainSecretStore();

        try
        {
            await store.SetAsync(service, account, "first-secret");
            Assert.Equal("first-secret", await store.GetAsync(service, account));

            await store.SetAsync(service, account, "rotated-secret");
            Assert.Equal("rotated-secret", await store.GetAsync(service, account));
        }
        finally
        {
            await store.DeleteAsync(service, account);
        }

        Assert.Null(await store.GetAsync(service, account));
    }
}
