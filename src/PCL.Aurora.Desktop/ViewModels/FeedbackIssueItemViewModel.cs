using PCL.Aurora.Application;

namespace PCL.Aurora.Desktop.ViewModels;

public sealed class FeedbackIssueItemViewModel
{
    public FeedbackIssueItemViewModel(GitHubIssue issue)
    {
        Issue = issue;
    }

    public GitHubIssue Issue { get; }

    public string Title => Issue.Title;

    public string Metadata => $"#{Issue.Number}  由 {Issue.Author} 提交于 {FormatRelativeTime(Issue.CreatedAt)}";

    public string TypeDisplay => string.IsNullOrWhiteSpace(Issue.TypeName) ? "反馈" : Issue.TypeName;

    public string LabelsDisplay => Issue.Labels.Count == 0 ? string.Empty : string.Join(" · ", Issue.Labels.Take(3));

    public string IconPath => Issue.Status switch
    {
        GitHubIssueStatus.Processing => "/Assets/Loaders/Pcl2CommandBlock.png",
        GitHubIssueStatus.Triage => "/Assets/Loaders/PclCeRedstoneBlock.png",
        GitHubIssueStatus.Waiting => "/Assets/Loaders/PclCeAnvil.png",
        GitHubIssueStatus.Paused => "/Assets/Loaders/PclCeRedstoneLampOff.png",
        GitHubIssueStatus.UpNext => "/Assets/Loaders/PclCeRedstoneLampOn.png",
        GitHubIssueStatus.Completed => "/Assets/Loaders/Pcl2Grass.png",
        _ => "/Assets/Loaders/PclCeCobbleStone.png",
    };

    private static string FormatRelativeTime(DateTimeOffset value)
    {
        var elapsed = DateTimeOffset.Now - value;
        if (elapsed < TimeSpan.Zero)
        {
            return value.LocalDateTime.ToString("yyyy/M/d");
        }

        if (elapsed.TotalMinutes < 1) return "刚刚";
        if (elapsed.TotalHours < 1) return $"{Math.Max(1, (int)elapsed.TotalMinutes)} 分钟前";
        if (elapsed.TotalDays < 1) return $"{Math.Max(1, (int)elapsed.TotalHours)} 小时前";
        if (elapsed.TotalDays < 30) return $"{Math.Max(1, (int)elapsed.TotalDays)} 天前";
        return value.LocalDateTime.ToString("yyyy/M/d");
    }
}
