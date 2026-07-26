// Directly adapted from PCL2, Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.vb.
// Copyright © 成都瓜皮龙科技有限公司.
// Modified by Micro-ATP for PCL Aurora: retains whitespace and quoted-token handling,
// but emits individual ArgumentList values instead of reconstructing a shell command line.
// See LICENSES/PCL2-LICENCE.txt and NOTICE.

using System.Text;

namespace PCL.Aurora.Domain;

public static class Pcl2MinecraftLegacyArgumentTokenizer
{
    public static bool TryTokenize(string arguments, out IReadOnlyList<string> tokens, out string? error)
    {
        tokens = [];
        error = null;
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return true;
        }

        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < arguments.Length; index++)
        {
            var character = arguments[index];
            if (character == '\\' && index + 1 < arguments.Length && arguments[index + 1] == '"')
            {
                current.Append('"');
                index++;
                continue;
            }

            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                AddCurrentToken(current, result);
                continue;
            }

            current.Append(character);
        }

        if (inQuotes)
        {
            error = "旧版 minecraftArguments 包含未闭合的双引号。";
            return false;
        }

        AddCurrentToken(current, result);
        tokens = result;
        return true;
    }

    private static void AddCurrentToken(StringBuilder current, List<string> result)
    {
        if (current.Length == 0)
        {
            return;
        }

        result.Add(current.ToString());
        current.Clear();
    }
}
