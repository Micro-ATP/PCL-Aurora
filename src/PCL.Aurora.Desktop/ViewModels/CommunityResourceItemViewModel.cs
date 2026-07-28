using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Aurora.Domain;

namespace PCL.Aurora.Desktop.ViewModels;

public sealed partial class CommunityResourceItemViewModel(CommunityResourceProject project) : ViewModelBase, IDisposable
{
    public CommunityResourceProject Project { get; } = project;

    public string Initial => Project.Initial;

    public bool HasIcon => Icon is not null;

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private Bitmap? icon;

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
