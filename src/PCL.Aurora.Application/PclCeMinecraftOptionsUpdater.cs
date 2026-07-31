using System.Globalization;

namespace PCL.Aurora.Application;

/// <summary>
/// Directly adapts PCL-CE ModLaunch language preparation for options.txt.
/// </summary>
public static class PclCeMinecraftOptionsUpdater
{
    private static readonly DateTimeOffset NoLanguageCutoff = new(2011, 11, 18, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LegacyCaseStart = new(2012, 1, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LegacyCaseEnd = new(2016, 6, 8, 23, 59, 59, TimeSpan.Zero);

    public static async Task UpdateLanguageAsync(
        string gameDirectory,
        DateTimeOffset? releaseTime,
        string launcherLanguage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        Directory.CreateDirectory(gameDirectory);
        var optionsPath = Path.Combine(gameDirectory, "options.txt");
        if (!File.Exists(optionsPath))
        {
            var yosbrOptions = Path.Combine(gameDirectory, "config", "yosbr", "options.txt");
            if (File.Exists(yosbrOptions))
            {
                optionsPath = yosbrOptions;
            }
        }

        var entries = await ReadEntriesAsync(optionsPath, cancellationToken).ConfigureAwait(false);
        var currentLanguage = GetValue(entries, "lang") ?? "none";
        var isUnconfigured = string.Equals(currentLanguage, "none", StringComparison.OrdinalIgnoreCase);
        var hasExistingSaves = Directory.Exists(Path.Combine(gameDirectory, "saves"));
        var sourceLanguage = isUnconfigured || !hasExistingSaves
            ? ResolveLauncherLanguage(launcherLanguage)
            : currentLanguage;
        var requiredLanguage = ResolveMinecraftLanguage(sourceLanguage, releaseTime);
        SetValue(entries, "lang", requiredLanguage);
        if ((isUnconfigured || !hasExistingSaves) && RequiresUnicodeFont(sourceLanguage))
        {
            SetValue(entries, "forceUnicodeFont", "true");
        }

        var temporaryPath = optionsPath + ".partial";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(optionsPath)!);
            await File.WriteAllLinesAsync(temporaryPath, entries, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, optionsPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
        }
    }

    public static string ResolveMinecraftLanguage(string? languageCode, DateTimeOffset? releaseTime)
    {
        if (releaseTime is { Year: > 2000 } && releaseTime <= NoLanguageCutoff)
        {
            return "none";
        }

        var normalized = string.IsNullOrWhiteSpace(languageCode)
            ? "none"
            : languageCode.Replace('-', '_').Trim();
        if (string.Equals(normalized, "none", StringComparison.OrdinalIgnoreCase))
        {
            return "none";
        }

        var segments = normalized.Split('_', 2, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return normalized.ToLowerInvariant();
        }

        var useLegacyRegionCase = releaseTime >= LegacyCaseStart && releaseTime <= LegacyCaseEnd;
        var region = useLegacyRegionCase ? segments[1].ToUpperInvariant() : segments[1].ToLowerInvariant();
        return $"{segments[0].ToLowerInvariant()}_{region}";
    }

    private static string ResolveLauncherLanguage(string configuredLanguage)
    {
        if (!string.Equals(configuredLanguage, LauncherLocalizationSettings.Auto, StringComparison.OrdinalIgnoreCase))
        {
            return configuredLanguage;
        }

        var current = CultureInfo.CurrentUICulture.Name;
        return string.IsNullOrWhiteSpace(current)
            ? LauncherLocalizationSettings.DefaultLanguageCode
            : current;
    }

    private static bool RequiresUnicodeFont(string languageCode) =>
        languageCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ||
        languageCode.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ||
        languageCode.StartsWith("ko", StringComparison.OrdinalIgnoreCase);

    private static async Task<List<string>> ReadEntriesAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        return (await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false)).ToList();
    }

    private static string? GetValue(IEnumerable<string> entries, string key)
    {
        var prefix = key + ":";
        return entries.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
    }

    private static void SetValue(List<string> entries, string key, string value)
    {
        var prefix = key + ":";
        var index = entries.FindIndex(line => line.StartsWith(prefix, StringComparison.Ordinal));
        if (index >= 0)
        {
            entries[index] = prefix + value;
        }
        else
        {
            entries.Add(prefix + value);
        }
    }
}
