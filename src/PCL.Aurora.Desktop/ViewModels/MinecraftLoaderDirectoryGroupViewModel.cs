using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.ViewModels;

public sealed partial class MinecraftLoaderDirectoryGroupViewModel : ViewModelBase
{
    public MinecraftLoaderDirectoryGroupViewModel(MinecraftLoaderKind kind, MinecraftLoaderDirectoryGroup group)
    {
        Kind = kind;
        Key = group.Key;
        Title = group.Title;
        IsCollapsible = group.IsCollapsible;
        IsLazy = group.IsLazy;
        IsExpanded = !group.IsCollapsible;
        if (group.IsLazy)
        {
            IsLoaded = false;
        }
        else
        {
            ReplaceEntries(group.Entries);
        }
    }

    public MinecraftLoaderKind Kind { get; }

    public string Key { get; }

    public string Title { get; }

    public bool IsCollapsible { get; }

    public bool IsLazy { get; }

    public ObservableCollection<MinecraftLoaderPackageItemViewModel> Entries { get; } = [];

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string error = string.Empty;

    [ObservableProperty]
    private bool isLoaded;

    public string StatusText => IsLoading ? $"正在获取 {Title} 的版本列表" : Error;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public void ReplaceEntries(IEnumerable<MinecraftLoaderPackageEntry> entries)
    {
        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(new(entry));
        }

        IsLoaded = true;
        IsLoading = false;
        Error = Entries.Count == 0 ? "暂无可用版本" : string.Empty;
    }
}

public sealed class MinecraftLoaderPackageItemViewModel(MinecraftLoaderPackageEntry package)
{
    public MinecraftLoaderPackageEntry Package { get; } = package;

    public MinecraftLoaderKind Kind => Package.Kind;

    public string DisplayName => Package.DisplayName;

    public string Information => Package.Information;

    public bool IsRecommended => Package.IsRecommended;

    public bool HasChangelog => Package.ChangelogUri is not null;

    public string IconPath => Package.Kind switch
    {
        MinecraftLoaderKind.Forge => "/Assets/Loaders/PclCeForge.png",
        MinecraftLoaderKind.NeoForge => "/Assets/Loaders/PclCeNeoForge.png",
        MinecraftLoaderKind.Fabric => "/Assets/Loaders/PclCeFabric.png",
        MinecraftLoaderKind.OptiFine => "/Assets/Loaders/PclCeOptiFine.png",
        MinecraftLoaderKind.Cleanroom => "/Assets/Loaders/PclCeCleanroom.png",
        MinecraftLoaderKind.LegacyFabric => "/Assets/Loaders/PclCeFabric.png",
        MinecraftLoaderKind.LabyMod => "/Assets/Loaders/PclCeLabyMod.png",
        MinecraftLoaderKind.LiteLoader => "/Assets/Loaders/PclCeLiteLoader.png",
        _ => "/Assets/Loaders/Pcl2Grass.png",
    };
}
