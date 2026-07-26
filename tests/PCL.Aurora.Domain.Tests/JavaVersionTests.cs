using PCL.Aurora.Domain;

namespace PCL.Aurora.Domain.Tests;

public sealed class JavaVersionTests
{
    [Theory]
    [InlineData("21.0.6", 21)]
    [InlineData("1.8.0_452", 8)]
    [InlineData("17", 17)]
    [InlineData("openjdk version 23.0.1", 23)]
    public void ParseMajorVersion_ParsesSupportedFormats(string value, int expected)
    {
        Assert.Equal(expected, JavaVersion.ParseMajorVersion(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    public void ParseMajorVersion_ReturnsNullForInvalidInput(string? value)
    {
        Assert.Null(JavaVersion.ParseMajorVersion(value));
    }
}
