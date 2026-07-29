using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.ViewModels;

public sealed partial class MinecraftInstallComponentViewModel(
    MinecraftLoaderKind kind,
    string displayName,
    string iconPath) : ViewModelBase
{
    public MinecraftLoaderKind Kind { get; } = kind;

    public string DisplayName { get; } = displayName;

    public string IconPath { get; } = iconPath;

    public ObservableCollection<MinecraftLoaderCatalogEntry> Versions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderSummary))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private MinecraftLoaderCatalogEntry? selectedVersion;

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderSummary))]
    [NotifyPropertyChangedFor(nameof(CanExpand))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderSummary))]
    [NotifyPropertyChangedFor(nameof(CanExpand))]
    private string unavailableReason = string.Empty;

    public bool HasSelection => SelectedVersion is not null;

    public bool CanExpand => !IsLoading && Versions.Count > 0;

    public string HeaderSummary => SelectedVersion is { } selected
        ? selected.Version
        : IsLoading
            ? "正在获取版本列表"
            : Versions.Count > 0
                ? "可添加"
                : string.IsNullOrWhiteSpace(UnavailableReason)
                    ? "暂无可用版本"
                    : UnavailableReason;

    public void ReplaceVersions(IEnumerable<MinecraftLoaderCatalogEntry> versions, string? unavailableReason = null)
    {
        Versions.Clear();
        foreach (var version in versions)
        {
            Versions.Add(version);
        }

        SelectedVersion = null;
        IsExpanded = false;
        IsLoading = false;
        UnavailableReason = Versions.Count == 0 ? unavailableReason ?? "暂无可用版本" : string.Empty;
        OnPropertyChanged(nameof(CanExpand));
        OnPropertyChanged(nameof(HeaderSummary));
    }

    public void ResetForLoading()
    {
        Versions.Clear();
        SelectedVersion = null;
        IsExpanded = false;
        UnavailableReason = string.Empty;
        IsLoading = true;
        OnPropertyChanged(nameof(CanExpand));
        OnPropertyChanged(nameof(HeaderSummary));
    }
}
