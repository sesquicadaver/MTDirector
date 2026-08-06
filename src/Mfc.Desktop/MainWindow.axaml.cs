using Avalonia.Controls;
using Mfc.Desktop.ViewModels;

namespace Mfc.Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(ShellViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }
}
