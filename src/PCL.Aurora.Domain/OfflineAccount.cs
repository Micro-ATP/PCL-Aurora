using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PCL.Aurora.Domain;

public static partial class OfflineAccount
{
    public static bool TryCreate(string? displayName, out MinecraftAccount? account)
    {
        account = null;
        if (string.IsNullOrWhiteSpace(displayName) || !PlayerNamePattern().IsMatch(displayName))
        {
            return false;
        }

        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"OfflinePlayer:{displayName}"));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        account = new MinecraftAccount(displayName, FormatUuid(hash), MinecraftAccountKind.Offline, true);
        return true;
    }

    private static string FormatUuid(byte[] value)
    {
        var hex = Convert.ToHexString(value).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }

    [GeneratedRegex("^[A-Za-z0-9_]{3,16}$")]
    private static partial Regex PlayerNamePattern();
}
