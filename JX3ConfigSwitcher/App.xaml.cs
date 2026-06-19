using System;
using System.Windows;

namespace JX3ConfigSwitcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var window = BackupWindowFactory.CreateWindow();
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
