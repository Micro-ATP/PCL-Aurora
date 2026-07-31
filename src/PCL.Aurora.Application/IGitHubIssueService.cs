namespace PCL.Aurora.Application;

public interface IGitHubIssueService
{
    Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(CancellationToken cancellationToken = default);
}
