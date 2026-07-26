using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class OfflineAccountTests
{
    [Fact]
    public void TryCreate_CreatesDeterministicOfflineAccount()
    {
        var created = OfflineAccount.TryCreate("Notch", out var account);

        Assert.True(created);
        Assert.NotNull(account);
        Assert.Equal(MinecraftAccountKind.Offline, account.Kind);
        Assert.Equal("b50ad385-829d-3141-a216-7e7d7539ba7f", account.Uuid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("bad-name")]
    [InlineData("this_name_is_too_long")]
    public void TryCreate_RejectsInvalidPlayerNames(string value)
    {
        Assert.False(OfflineAccount.TryCreate(value, out var account));
        Assert.Null(account);
    }
}
