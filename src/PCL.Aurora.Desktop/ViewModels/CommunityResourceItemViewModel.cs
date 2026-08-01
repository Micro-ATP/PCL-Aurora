using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Aurora.Application;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.ViewModels;

public sealed partial class CommunityResourceItemViewModel(
    CommunityResourceProject project,
    CommunityModNameStyle initialNameStyle = CommunityModNameStyle.TranslationTitle) : ViewModelBase, IDisposable
{
    public CommunityResourceProject Project { get; } = project;

    public string Initial => Project.Initial;

    public bool CanQuickDownload => Project.Type != CommunityResourceType.ModPack;

    public string DisplayTitle => nameStyle == CommunityModNameStyle.FileNameTitle
        ? Project.Title
        : Project.DisplayTitle;

    public string SecondaryTitle
    {
        get
        {
            if (!Project.HasTranslatedTitle)
            {
                return string.Empty;
            }

            return nameStyle == CommunityModNameStyle.FileNameTitle
                ? $"  |  {Project.DisplayTitle}"
                : Project.OriginalTitleDisplay;
        }
    }

    public bool HasIcon => Icon is not null;

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private Bitmap? icon;

    private CommunityModNameStyle nameStyle = initialNameStyle;

    public void ApplyNameStyle(CommunityModNameStyle value)
    {
        if (nameStyle == value)
        {
            return;
        }

        nameStyle = value;
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(SecondaryTitle));
    }

    public void SetIcon(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        Icon = new Bitmap(stream);
    }

    public void Dispose()
    {
        Icon = null;
    }

    partial void OnIconChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        oldValue?.Dispose();
        OnPropertyChanged(nameof(HasIcon));
    }
}
