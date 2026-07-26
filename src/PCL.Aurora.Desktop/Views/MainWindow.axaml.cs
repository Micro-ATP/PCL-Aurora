using Avalonia.Controls;

namespace PCL.Aurora.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            if (DataContext is ViewModels.MainViewModel viewModel)
            {
                await viewModel.RefreshCommand.ExecuteAsync(null);
            }
        };
    }
}
