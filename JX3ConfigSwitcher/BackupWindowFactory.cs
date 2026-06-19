using System.Windows;
using System.Windows.Controls;
using JX3ConfigSwitcher.Services;
using JX3ConfigSwitcher.ViewModels;
using JX3ConfigSwitcher.Views;

namespace JX3ConfigSwitcher;

public static class BackupWindowFactory
{
    public static MainWindow CreateWindow(string? baseDirectory = null, IBackupProfileHostApi? profileHostApi = null)
    {
        var viewModel = BuildViewModel(baseDirectory, profileHostApi);
        return new MainWindow(viewModel)
        {
            Width = viewModel.WindowWidth,
            Height = viewModel.WindowHeight
        };
    }

    public static UserControl CreateView(string? baseDirectory = null, IBackupProfileHostApi? hostApi = null)
    {
        var viewModel = BuildViewModel(baseDirectory, hostApi);
        return new BackupView
        {
            DataContext = viewModel
        };
    }

    private static MainViewModel BuildViewModel(string? baseDirectory = null, IBackupProfileHostApi? profileHostApi = null)
    {
        var paths = new PortablePaths(baseDirectory);
        paths.EnsureCreated();

        var settings = new SettingsService(paths);
        settings.Load();

        var database = new DatabaseService(paths);
        database.Initialize();

        var repository = new ProfileRepository(database);
        var scanner = new GameScanner();
        var classifier = new ConfigClassifier();
        var processGuard = new GameProcessGuard();
        var cndkLuaFile = new CndkLuaFile();
        var skillPlacementService = new SkillPlacementService(cndkLuaFile);
        var backupService = new BackupService(paths, repository, classifier, processGuard, skillPlacementService);
        var syncService = new SyncService(repository);
        var viewModel = new MainViewModel(paths, settings, repository, scanner, classifier, backupService, syncService, profileHostApi);
        viewModel.Initialize();
        return viewModel;
    }

    public static MainWindow ShowWindow(Window? owner = null, string? baseDirectory = null, IBackupProfileHostApi? profileHostApi = null)
    {
        var window = CreateWindow(baseDirectory, profileHostApi);
        if (owner is not null)
        {
            window.Owner = owner;
        }

        window.Show();
        return window;
    }
}
