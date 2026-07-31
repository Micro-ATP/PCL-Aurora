using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Aurora.Application;

namespace PCL.Aurora.Desktop.ViewModels;

public partial class FeedbackGroupViewModel(
    GitHubIssueStatus status,
    string title,
    bool isExpanded) : ObservableObject
{
    public GitHubIssueStatus Status { get; } = status;

    public string Title { get; } = title;

    public ObservableCollection<FeedbackIssueItemViewModel> Issues { get; } = [];

    [ObservableProperty]
    private bool isExpanded = isExpanded;

    [ObservableProperty]
    private bool hasItems;

    public void ReplaceIssues(IEnumerable<GitHubIssue> issues)
    {
        Issues.Clear();
        foreach (var issue in issues)
        {
            Issues.Add(new FeedbackIssueItemViewModel(issue));
        }

        HasItems = Issues.Count > 0;
    }
}
