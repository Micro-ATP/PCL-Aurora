using CommunityToolkit.Mvvm.ComponentModel;

namespace PCL.Aurora.Desktop.ViewModels;

public sealed partial class DownloadStageItemViewModel(string title) : ViewModelBase
{
    public string Title { get; } = title;

    [ObservableProperty]
    private string progressText = string.Empty;

    [ObservableProperty]
    private string statusGlyph = string.Empty;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool isCompleted;

    [ObservableProperty]
    private bool isFailed;

    public void Reset()
    {
        ProgressText = string.Empty;
        StatusGlyph = string.Empty;
        IsActive = false;
        IsCompleted = false;
        IsFailed = false;
    }

    public void Start(string? progress = null)
    {
        Reset();
        IsActive = true;
        StatusGlyph = "•••";
        ProgressText = progress ?? string.Empty;
    }

    public void Complete()
    {
        Reset();
        IsCompleted = true;
        StatusGlyph = "✓";
    }

    public void Fail()
    {
        IsActive = false;
        IsFailed = true;
        StatusGlyph = "!";
    }
}
