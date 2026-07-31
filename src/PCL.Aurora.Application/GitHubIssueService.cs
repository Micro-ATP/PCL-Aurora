using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCL.Aurora.Application;

public sealed class GitHubIssueService(HttpClient httpClient) : IGitHubIssueService
{
    private static readonly Uri IssuesUri =
        new("https://api.github.com/repos/Micro-ATP/PCL-Aurora/issues?state=all&sort=created&direction=desc&per_page=100&page=1");
    private static readonly ProductInfoHeaderValue UserAgent = new("PCL-Aurora", "1.0");

    public async Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(CancellationToken cancellationToken = default)
    {
        var firstPage = await GetPageAsync(IssuesUri, cancellationToken).ConfigureAwait(false);
        var secondPageUri = new Uri(IssuesUri.AbsoluteUri.Replace("page=1", "page=2", StringComparison.Ordinal));
        var secondPage = firstPage.Count == 100
            ? await GetPageAsync(secondPageUri, cancellationToken).ConfigureAwait(false)
            : [];

        return firstPage
            .Concat(secondPage)
            .Where(entry => entry.PullRequest is null)
            .Take(200)
            .Select(entry => new GitHubIssue(
                entry.Number,
                entry.Title,
                entry.User?.Login ?? "未知用户",
                entry.CreatedAt,
                entry.UpdatedAt,
                string.IsNullOrWhiteSpace(entry.Body) ? "该反馈没有填写详细内容。" : entry.Body.Trim(),
                entry.HtmlUri,
                entry.Type?.Name,
                entry.Labels.Select(label => label.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray(),
                ClassifyStatus(entry.Labels.Select(label => label.Name))))
            .ToArray();
    }

    internal static GitHubIssueStatus ClassifyStatus(IEnumerable<string> labels)
    {
        var normalized = labels.Select(NormalizeLabel).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in StatusLabels)
        {
            if (mapping.Value.Any(normalized.Contains))
            {
                return mapping.Key;
            }
        }

        return GitHubIssueStatus.Triage;
    }

    private async Task<List<IssueResponse>> GetPageAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<List<IssueResponse>>(
                   content,
                   cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
    }

    private static string NormalizeLabel(string value) =>
        value.Trim().Replace('_', ' ').Replace('-', ' ').ToLowerInvariant();

    private static readonly IReadOnlyDictionary<GitHubIssueStatus, string[]> StatusLabels =
        new Dictionary<GitHubIssueStatus, string[]>
        {
            [GitHubIssueStatus.Processing] = ["正在处理", "processing", "in progress"],
            [GitHubIssueStatus.Triage] = ["等待处理", "triage", "needs triage"],
            [GitHubIssueStatus.Waiting] = ["等待", "waiting", "blocked"],
            [GitHubIssueStatus.Paused] = ["暂停", "paused", "on hold"],
            [GitHubIssueStatus.UpNext] = ["在即", "up next", "next"],
            [GitHubIssueStatus.Completed] = ["已完成", "completed", "done", "fixed"],
            [GitHubIssueStatus.Declined] = ["已拒绝", "declined", "rejected", "wontfix", "won't fix"],
            [GitHubIssueStatus.Ignored] = ["已忽略", "ignored", "invalid"],
            [GitHubIssueStatus.Duplicate] = ["重复", "duplicate"],
        };

    private sealed record IssueResponse(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("html_url")] Uri HtmlUri,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
        [property: JsonPropertyName("user")] UserResponse? User,
        [property: JsonPropertyName("labels")] IReadOnlyList<LabelResponse> Labels,
        [property: JsonPropertyName("type")] IssueTypeResponse? Type,
        [property: JsonPropertyName("pull_request")] JsonElement? PullRequest);

    private sealed record UserResponse([property: JsonPropertyName("login")] string Login);
    private sealed record LabelResponse([property: JsonPropertyName("name")] string Name);
    private sealed record IssueTypeResponse([property: JsonPropertyName("name")] string Name);
}
