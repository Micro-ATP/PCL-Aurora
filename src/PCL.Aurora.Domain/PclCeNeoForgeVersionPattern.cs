// Directly adapted from PCL-CE, PCL.Core/Utils/RegexPatterns.cs.
// Licensed under Apache License 2.0. Copyright notices and license text are retained in NOTICE and LICENSES/Apache-2.0.txt.
// Modified by Micro-ATP: isolated the single NeoForge catalog pattern for PCL Aurora's loader parser.

using System.Text.RegularExpressions;

namespace PCL.Aurora.Domain;

internal static partial class PclCeNeoForgeVersionPattern
{
    [GeneratedRegex(@"(?<="")(1\.20\.1-)?\d+\.[^\.]+\.\d+(\.\d+)?(-(beta|alpha)(\.\d+)?)?(\+snapshot-\d+)?(\+pre-\d+)?(?="")", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    internal static partial Regex Create();

    internal static bool IsMatch(string version) => Create().IsMatch($"\"{version}\"");
}
