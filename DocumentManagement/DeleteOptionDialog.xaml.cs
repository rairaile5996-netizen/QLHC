using System.Windows;
using System.Windows.Media;
using DocumentManagement.Models;

namespace DocumentManagement
{
    public partial class DeleteOptionDialog : Window
    {
    public DeleteOption SelectedOption { get; private set; } = DeleteOption.Cancel;

    public DeleteOptionDialog(bool isDarkTheme = false)
    {
        InitializeComponent();
        ApplyTheme(isDarkTheme);
    }

    private void ApplyTheme(bool isDark)
    {
        if (isDark)
        {
            MainBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
            TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
            MessageText.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            BtnCancel.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        }
        else
        {
            MainBorder.Background = Brushes.White;
            MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
            TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            MessageText.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            BtnCancel.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
        }
    }

    private void BtnDeleteAll_Click(object sender, RoutedEventArgs e)
    {
        SelectedOption = DeleteOption.DeleteAll;
        DialogResult = true;
        Close();
    }

    private void BtnDeleteAppOnly_Click(object sender, RoutedEventArgs e)
    {
        SelectedOption = DeleteOption.DeleteAppOnly;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedOption = DeleteOption.Cancel;
        DialogResult = false;
        Close();
    }
    }
}
