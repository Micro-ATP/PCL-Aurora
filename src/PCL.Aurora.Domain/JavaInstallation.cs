namespace PCL.Aurora.Domain;

public sealed record JavaInstallation(
    string ExecutablePath,
    string? Version,
    int? MajorVersion,
    string Vendor,
    JavaArchitecture Architecture,
    JavaSource Source,
    bool IsCompatible)
{
    public Version? ParsedVersion => TryParseVersion(Version);

    private static Version? TryParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("1.", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        var updateSeparator = normalized.IndexOfAny(['u', 'U', '_']);
        var update = 0;
        if (updateSeparator >= 0)
        {
            if (!int.TryParse(normalized[(updateSeparator + 1)..], out update))
            {
                return null;
            }

            normalized = normalized[..updateSeparator];
        }

        var numericSegments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (numericSegments.Length == 0 || numericSegments.Length > 3 ||
            numericSegments.Any(segment => !int.TryParse(segment, out _)))
        {
            return null;
        }

        var major = int.Parse(numericSegments[0], System.Globalization.CultureInfo.InvariantCulture);
        var minor = numericSegments.Length > 1
            ? int.Parse(numericSegments[1], System.Globalization.CultureInfo.InvariantCulture)
            : 0;
        var build = update > 0
            ? update
            : numericSegments.Length > 2
                ? int.Parse(numericSegments[2], System.Globalization.CultureInfo.InvariantCulture)
                : 0;
        return new Version(major, minor, build);
    }
}
