using System.Net;
using System.Text;
using PCL.Aurora.Application;

namespace PCL.Aurora.Application.Tests;

public sealed class GitHubIssueServiceTests
{
    [Fact]
    public async Task GetIssuesAsync_ExcludesPullRequestsAndMapsStatusByLabelName()
    {
        var service = CreateService("""
            [
              {
                "number":12,
                "title":"启动失败",
                "body":"复现步骤",
                "html_url":"https://github.com/Micro-ATP/PCL-Aurora/issues/12",
                "created_at":"2026-07-30T00:00:00Z",
                "updated_at":"2026-07-30T01:00:00Z",
                "user":{"login":"tester"},
                "labels":[{"name":"正在处理"},{"name":"bug"}],
                "type":{"name":"Bug"}
              },
              {
                "number":13,
                "title":"pull request",
                "body":"",
                "html_url":"https://github.com/Micro-ATP/PCL-Aurora/pull/13",
                "created_at":"2026-07-30T00:00:00Z",
                "updated_at":"2026-07-30T01:00:00Z",
                "user":{"login":"tester"},
                "labels":[],
                "pull_request":{"url":"https://api.github.com/repos/Micro-ATP/PCL-Aurora/pulls/13"}
              }
            ]
            """);

        var issue = Assert.Single(await service.GetIssuesAsync());

        Assert.Equal(12, issue.Number);
        Assert.Equal("tester", issue.Author);
        Assert.Equal("Bug", issue.TypeName);
        Assert.Equal(GitHubIssueStatus.Processing, issue.Status);
        Assert.Contains("bug", issue.Labels);
    }

    [Fact]
    public async Task GetIssuesAsync_UnmatchedStatusFallsBackToTriage()
    {
        var service = CreateService("""
            [{
              "number":21,
              "title":"建议",
              "body":null,
              "html_url":"https://github.com/Micro-ATP/PCL-Aurora/issues/21",
              "created_at":"2026-07-30T00:00:00Z",
              "updated_at":"2026-07-30T01:00:00Z",
              "user":{"login":"tester"},
              "labels":[{"name":"enhancement"}],
              "type":null
            }]
            """);

        var issue = Assert.Single(await service.GetIssuesAsync());

        Assert.Equal(GitHubIssueStatus.Triage, issue.Status);
        Assert.Equal("该反馈没有填写详细内容。", issue.Body);
    }

    private static GitHubIssueService CreateService(string responseJson) =>
        new(new HttpClient(new StaticResponseHandler(responseJson)));

    private sealed class StaticResponseHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
    }
}
