using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class Pcl2MinecraftLegacyArgumentTokenizerTests
{
    [Fact]
    public void TryTokenize_PreservesWhitespaceInsideQuotesAndEscapedQuotes()
    {
        var success = Pcl2MinecraftLegacyArgumentTokenizer.TryTokenize(
            "--gameDir \"/Minecraft Folder\" --note \"A \\\"quoted\\\" value\"",
            out var tokens,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(["--gameDir", "/Minecraft Folder", "--note", "A \"quoted\" value"], tokens);
    }

    [Fact]
    public void TryTokenize_RejectsUnclosedQuote()
    {
        var success = Pcl2MinecraftLegacyArgumentTokenizer.TryTokenize(
            "--gameDir \"/Minecraft Folder",
            out var tokens,
            out var error);

        Assert.False(success);
        Assert.Empty(tokens);
        Assert.Contains("未闭合", error);
    }
}
