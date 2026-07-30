using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCL.Aurora.Application;

public sealed class GitHubLauncherUpdateService(HttpClient httpClient) : ILauncherUpdateService
{
    private static readonly Uri ReleasesUri =
        new("https://api.github.com/repos/Micro-ATP/PCL-Aurora/releases?per_page=30");
    private static readonly ProductInfoHeaderValue UserAgent = new("PCL-Aurora", "1.0");

    public async Task<LauncherUpdateCheckResult> CheckAsync(
        string currentVersion,
        LauncherUpdateChannel channel,
        CancellationToken cancellationToken = default)
    {
        if (!ReleaseVersion.TryParse(currentVersion, out var parsedCurrentVersion))
        {
            throw new InvalidDataException($"无法识别当前版本号：{currentVersion}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesUri);
        request.Headers.UserAgent.Add(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var entries = await JsonSerializer.DeserializeAsync<List<ReleaseResponse>>(
            content,
            cancellationToken: cancellationToken) ?? [];

        var release = entries
            .Where(entry => !entry.Draft && (channel == LauncherUpdateChannel.Beta || !entry.Prerelease))
            .Select(entry => (Entry: entry, Parsed: ReleaseVersion.TryParse(entry.TagName, out var version) ? version : null))
            .Where(candidate => candidate.Parsed is not null)
            .OrderByDescending(candidate => candidate.Parsed)
            .FirstOrDefault();

        if (release.Entry is null || release.Parsed is null)
        {
            throw new InvalidDataException("暂未找到可用的 PCL Aurora 发行版本。");
        }

        var versionName = release.Parsed.ToString();
        var changelog = string.IsNullOrWhiteSpace(release.Entry.Body)
            ? "本次更新暂无更新日志。"
            : release.Entry.Body.Trim();
        var displayName = $"PCL Aurora {FormatVersionName(versionName)}";
        var latestRelease = new LauncherUpdateRelease(
            versionName,
            displayName,
            CreateSummary(changelog),
            changelog,
            release.Entry.ReleaseUri,
            release.Entry.PublishedAt);

        return new LauncherUpdateCheckResult(release.Parsed.CompareTo(parsedCurrentVersion) > 0, latestRelease);
    }

    internal static string CreateSummary(string changelog)
    {
        var lines = changelog
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(line => line.TrimStart('#', '-', '*', ' '))
            .Where(line => line.Length > 0)
            .Take(3)
            .ToArray();
        var summary = string.Join(Environment.NewLine, lines);
        if (summary.Length <= 360)
        {
            return summary;
        }

        return string.Concat(summary.AsSpan(0, 359).TrimEnd(), "…");
    }

    internal static string FormatVersionName(string versionName)
    {
        var normalized = versionName.TrimStart('v', 'V');
        var separator = normalized.IndexOf('-');
        if (separator < 0)
        {
            return normalized;
        }

        var suffix = normalized[(separator + 1)..]
            .Replace("beta", "Beta", StringComparison.OrdinalIgnoreCase)
            .Replace("rc", "RC", StringComparison.OrdinalIgnoreCase)
            .Replace('.', ' ');
        return $"{normalized[..separator]} {suffix}";
    }

    private sealed record ReleaseResponse(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("html_url")] Uri ReleaseUri,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("published_at")] DateTimeOffset PublishedAt);

    private sealed record ReleaseVersion(
        int Major,
        int Minor,
        int Patch,
        string? Prerelease) : IComparable<ReleaseVersion>
    {
        public static bool TryParse(string? value, out ReleaseVersion? version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().TrimStart('v', 'V');
            var metadataIndex = normalized.IndexOf('+');
            if (metadataIndex >= 0)
            {
                normalized = normalized[..metadataIndex];
            }

            var prereleaseIndex = normalized.IndexOf('-');
            var prerelease = prereleaseIndex >= 0 ? normalized[(prereleaseIndex + 1)..] : null;
            var core = prereleaseIndex >= 0 ? normalized[..prereleaseIndex] : normalized;
            var parts = core.Split('.');
            var patch = 0;
            if (parts.Length is < 2 or > 4 ||
                !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor) ||
                (parts.Length > 2 && !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch)))
            {
                return false;
            }

            version = new ReleaseVersion(major, minor, patch, prerelease);
            return true;
        }

        public int CompareTo(ReleaseVersion? other)
        {
            if (other is null)
            {
                return 1;
            }

            var coreComparison = Major.CompareTo(other.Major);
            if (coreComparison == 0) coreComparison = Minor.CompareTo(other.Minor);
            if (coreComparison == 0) coreComparison = Patch.CompareTo(other.Patch);
            if (coreComparison != 0) return coreComparison;
            if (Prerelease is null) return other.Prerelease is null ? 0 : 1;
            if (other.Prerelease is null) return -1;
            return ComparePrerelease(Prerelease, other.Prerelease);
        }

        public override string ToString() =>
            Prerelease is null
                ? $"{Major}.{Minor}.{Patch}"
                : $"{Major}.{Minor}.{Patch}-{Prerelease}";

        private static int ComparePrerelease(string left, string right)
        {
            var leftParts = left.Split('.');
            var rightParts = right.Split('.');
            for (var index = 0; index < Math.Max(leftParts.Length, rightParts.Length); index++)
            {
                if (index >= leftParts.Length) return -1;
                if (index >= rightParts.Length) return 1;
                var leftNumeric = int.TryParse(leftParts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
                var rightNumeric = int.TryParse(rightParts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
                var comparison = leftNumeric && rightNumeric
                    ? leftNumber.CompareTo(rightNumber)
                    : leftNumeric
                        ? -1
                        : rightNumeric
                            ? 1
                            : string.Compare(leftParts[index], rightParts[index], StringComparison.OrdinalIgnoreCase);
                if (comparison != 0) return comparison;
            }

            return 0;
        }
    }
}
