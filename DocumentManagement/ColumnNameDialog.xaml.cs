using System.Windows;
using System.Windows.Input;

namespace DocumentManagement;

public partial class ColumnNameDialog : Window
{
    public string ColumnName => NameTextBox.Text.Trim();

    public ColumnNameDialog(string title, string message, string defaultValue)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        NameTextBox.Text = defaultValue;
        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    public static string? Prompt(string title, string message, string defaultValue, Window? owner = null)
    {
        var dialog = new ColumnNameDialog(title, message, defaultValue)
        {
            Owner = owner ?? Application.Current?.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.ColumnName : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ColumnName))
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void NameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Ok_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Cancel_Click(sender, e);
            e.Handled = true;
        }
    }
}
