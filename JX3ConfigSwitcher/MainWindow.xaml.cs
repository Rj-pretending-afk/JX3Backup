using System.Windows;
using JX3ConfigSwitcher.Services;
using JX3ConfigSwitcher.ViewModels;

namespace JX3ConfigSwitcher;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;

    public MainWindow(MainViewModel viewModel, SettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _settingsService.Settings.WindowWidth = Width;
        _settingsService.Settings.WindowHeight = Height;
        _settingsService.Save();
        base.OnClosing(e);
    }
}
