using CommunityToolkit.Mvvm.ComponentModel;

namespace PCL.Aurora.Desktop.ViewModels;

public sealed partial class CommunityResourceVersionFilterOption(
    string value,
    string displayName,
    bool isSelected = false) : ViewModelBase
{
    public string Value { get; } = value;

    public string DisplayName { get; } = displayName;

    [ObservableProperty]
    private bool isSelected = isSelected;
}
