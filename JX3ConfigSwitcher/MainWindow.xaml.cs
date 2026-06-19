using System.Windows;
using JX3ConfigSwitcher.ViewModels;

namespace JX3ConfigSwitcher;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SaveAndDispose(Width, Height);
        }

        base.OnClosing(e);
    }
}
