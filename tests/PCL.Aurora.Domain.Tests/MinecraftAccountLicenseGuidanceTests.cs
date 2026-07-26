using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class MinecraftAccountLicenseGuidanceTests
{
    [Fact]
    public void Evaluate_RequiresExplicitAcknowledgementForOfflineAccounts()
    {
        OfflineAccount.TryCreate("AuroraPlayer", out var account);

        var guidance = MinecraftAccountLicenseGuidance.Evaluate(account);

        Assert.True(guidance.RequiresAcknowledgement);
        Assert.Contains("不能证明", guidance.Message, StringComparison.Ordinal);
        Assert.Equal(MinecraftAccountLicenseGuidance.MinecraftPurchaseUri, guidance.PurchaseUri!.OriginalString);
    }

    [Fact]
    public void Evaluate_DoesNotRequireAcknowledgementForAuthenticatedMicrosoftAccounts()
    {
        var account = new MinecraftAccount(
            "Alex",
            "00000000-0000-0000-0000-000000000000",
            MinecraftAccountKind.Microsoft,
            IsAuthenticated: true);

        var guidance = MinecraftAccountLicenseGuidance.Evaluate(account);

        Assert.False(guidance.RequiresAcknowledgement);
        Assert.Null(guidance.PurchaseUri);
    }

    [Fact]
    public void ToString_DoesNotExposeInMemoryMicrosoftAccessToken()
    {
        var account = new MinecraftAccount(
            "Alex",
            "00000000-0000-0000-0000-000000000000",
            MinecraftAccountKind.Microsoft,
            IsAuthenticated: true)
        {
            AccessToken = "private-minecraft-access-token",
        };

        var representation = account.ToString();

        Assert.DoesNotContain("private-minecraft-access-token", representation, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken", representation, StringComparison.Ordinal);
    }
}
