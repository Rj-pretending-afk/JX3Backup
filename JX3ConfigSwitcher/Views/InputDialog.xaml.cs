using System.Windows;

namespace JX3ConfigSwitcher.Views;

public partial class InputDialog : Window
{
    private InputDialog(string title, string prompt, string defaultValue)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        ValueBox.Text = defaultValue;
        ValueBox.SelectAll();
        ValueBox.Focus();
    }

    public string Value => ValueBox.Text;

    public static string? Ask(string title, string prompt, string defaultValue)
    {
        var dialog = new InputDialog(title, prompt, defaultValue)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
