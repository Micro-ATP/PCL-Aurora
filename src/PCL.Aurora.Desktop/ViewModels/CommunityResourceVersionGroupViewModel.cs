using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.ViewModels;

public sealed partial class CommunityResourceVersionGroupViewModel(
    string title,
    IReadOnlyList<CommunityResourceVersion> versions,
    bool isExpanded) : ViewModelBase
{
    public string Title { get; } = title;

    public IReadOnlyList<CommunityResourceVersion> Versions { get; } = versions;

    public int Count => Versions.Count;

    [ObservableProperty]
    private bool isExpanded = isExpanded;
}
