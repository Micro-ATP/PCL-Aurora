using System.Text.RegularExpressions;

namespace PCL.Aurora.Domain;

public static partial class JavaVersion
{
    public static int? ParseMajorVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = VersionPattern().Match(value);
        if (!match.Success)
        {
            return null;
        }

        var first = int.Parse(match.Groups["first"].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (first == 1 && int.TryParse(match.Groups["second"].Value, out var legacyMajor))
        {
            return legacyMajor;
        }

        return first;
    }

    [GeneratedRegex("(?<first>\\d+)(?:\\.(?<second>\\d+))?")]
    private static partial Regex VersionPattern();
}
