// Directly adapted from PCL-CE, Plain Craft Launcher 2/Modules/Minecraft/McVersionComparer.cs.
// Copyright © 成都瓜皮龙科技有限公司.
// Modified by Micro-ATP for PCL Aurora: removed PCL UI/localization helpers and
// retained the platform-independent version-token comparison algorithm.
// See LICENSES/PCL-CE-Plain-Craft-Launcher-2-LICENCE.txt and NOTICE.

using System.Globalization;
using System.Text.RegularExpressions;

namespace PCL.Aurora.Domain;

public static class PclCeVersionComparer
{
    public const string UnknownVersionKey = "UnknownVersion";

    private static readonly Regex VersionTokenPattern = new("[a-z]+|[0-9]+", RegexOptions.CultureInvariant);

    public static bool CompareVersionGe(string left, string right) => CompareVersion(left, right) >= 0;

    public static int CompareVersion(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left == UnknownVersionKey || right == UnknownVersionKey)
        {
            return (left, right) switch
            {
                (UnknownVersionKey, UnknownVersionKey) => 0,
                (UnknownVersionKey, _) => 1,
                _ => -1,
            };
        }

        left = left.ToLowerInvariant();
        right = right.ToLowerInvariant();
        var leftTokens = GetVersionTokens(left);
        var rightTokens = GetVersionTokens(right);

        for (var index = 0; ; index++)
        {
            if (index >= leftTokens.Count && index >= rightTokens.Count)
            {
                return string.CompareOrdinal(left, right);
            }

            var leftValue = index >= leftTokens.Count ? "0" : NormalizePrereleaseToken(leftTokens[index]);
            var rightValue = index >= rightTokens.Count ? "0" : NormalizePrereleaseToken(rightTokens[index]);
            if (leftValue == rightValue)
            {
                continue;
            }

            var leftIsNumber = double.TryParse(leftValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var leftNumber);
            var rightIsNumber = double.TryParse(rightValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var rightNumber);
            if (!leftIsNumber && !rightIsNumber)
            {
                var comparison = string.CompareOrdinal(leftValue, rightValue);
                if (comparison != 0)
                {
                    return comparison > 0 ? 1 : -1;
                }

                continue;
            }

            if (leftNumber != rightNumber)
            {
                return leftNumber > rightNumber ? 1 : -1;
            }
        }
    }

    private static IReadOnlyList<string> GetVersionTokens(string value) =>
        VersionTokenPattern.Matches(value).Select(match => match.Value).ToArray();

    private static string NormalizePrereleaseToken(string value) => value switch
    {
        "rc" => "-1",
        "pre" => "-2",
        "snapshot" => "-3",
        "experimental" => "-4",
        _ => value,
    };

    public sealed class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y) => CompareVersion(x ?? string.Empty, y ?? string.Empty);
    }
}
