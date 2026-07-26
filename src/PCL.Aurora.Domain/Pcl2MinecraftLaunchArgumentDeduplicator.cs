// Directly adapted from PCL2, Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb.
// Copyright © 成都瓜皮龙科技有限公司.
// Modified by Micro-ATP for PCL Aurora: operates only on already-tokenized ArgumentList
// values and never reconstructs a shell command line.
// See LICENSES/PCL2-LICENCE.txt and NOTICE.

using System.Globalization;

namespace PCL.Aurora.Domain;

public static class Pcl2MinecraftLaunchArgumentDeduplicator
{
    public static IReadOnlyList<string> Deduplicate(IReadOnlyList<string> arguments, bool isJvmArgument)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var result = new List<string>(arguments.Count);
        for (var index = 0; index < arguments.Count;)
        {
            var key = arguments[index];
            if (IsSingleArgument(arguments, index))
            {
                index++;
                if (!result.Contains(key, StringComparer.Ordinal))
                {
                    result.Add(key);
                }

                continue;
            }

            var value = arguments[index + 1];
            index += 2;
            var handled = false;
            for (var resultIndex = 0; resultIndex + 1 < result.Count; resultIndex++)
            {
                if (!string.Equals(result[resultIndex], key, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!isJvmArgument && !string.Equals(key, "--tweakClass", StringComparison.Ordinal))
                {
                    result[resultIndex + 1] = value;
                    handled = true;
                    break;
                }

                if (string.Equals(result[resultIndex + 1], value, StringComparison.Ordinal))
                {
                    handled = true;
                    break;
                }
            }

            if (!handled)
            {
                result.Add(key);
                result.Add(value);
            }
        }

        return result;
    }

    private static bool IsSingleArgument(IReadOnlyList<string> arguments, int index)
    {
        var key = arguments[index];
        if (!key.StartsWith("-", StringComparison.Ordinal) || index + 1 >= arguments.Count)
        {
            return true;
        }

        var next = arguments[index + 1];
        return next.StartsWith("-", StringComparison.Ordinal) &&
               !double.TryParse(next[1..], NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
}
