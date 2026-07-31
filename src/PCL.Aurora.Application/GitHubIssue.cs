namespace PCL.Aurora.Application;

public enum GitHubIssueStatus
{
    Processing,
    Triage,
    Waiting,
    Paused,
    UpNext,
    Completed,
    Declined,
    Ignored,
    Duplicate,
}

public sealed record GitHubIssue(
    int Number,
    string Title,
    string Author,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Body,
    Uri IssueUri,
    string? TypeName,
    IReadOnlyList<string> Labels,
    GitHubIssueStatus Status);
