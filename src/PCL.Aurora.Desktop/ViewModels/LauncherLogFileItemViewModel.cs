using PCL.Aurora.Application;

namespace PCL.Aurora.Desktop.ViewModels;

public sealed class LauncherLogFileItemViewModel
{
    public LauncherLogFileItemViewModel(LauncherLogFile file)
    {
        File = file;
    }

    public LauncherLogFile File { get; }

    public string Title => File.IsCurrent ? $"{File.Name} (当前)" : File.Name;

    public string Metadata => $"{File.ModifiedAt.LocalDateTime:yyyy/M/d HH:mm:ss}  ·  {FormatSize(File.Length)}";

    private static string FormatSize(long length) => length switch
    {
        < 1024 => $"{length} B",
        < 1024 * 1024 => $"{length / 1024d:0.#} KB",
        _ => $"{length / 1024d / 1024d:0.#} MB",
    };
}
