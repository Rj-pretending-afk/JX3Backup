using System;
using System.Windows;
using JX3ConfigSwitcher.Services;
using JX3ConfigSwitcher.ViewModels;

namespace JX3ConfigSwitcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var paths = new PortablePaths();
            paths.EnsureCreated();

            var settings = new SettingsService(paths);
            settings.Load();

            var database = new DatabaseService(paths);
            database.Initialize();

            var repository = new ProfileRepository(database);
            var scanner = new GameScanner();
            var classifier = new ConfigClassifier();
            var processGuard = new GameProcessGuard();
            var backupService = new BackupService(paths, repository, classifier, processGuard);
            var syncService = new SyncService(repository);
            var viewModel = new MainViewModel(paths, settings, repository, scanner, classifier, backupService, syncService);
            viewModel.Initialize();

            var window = new MainWindow(viewModel, settings)
            {
                Width = settings.Settings.WindowWidth,
                Height = settings.Settings.WindowHeight
            };
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"剑3备份器启动失败。\n\n{ex.Message}\n\n请确认程序所在目录可写，或把整个文件夹移动到普通用户目录。",
                "启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
