using System.Windows;
using System.Windows.Input;
using DocumentManagement.ViewModels;

namespace DocumentManagement.Views;

public partial class BoqImportWindow : Window
{
    public BoqImportWindow() => InitializeComponent();

    private BoqImportViewModel? Vm => DataContext as BoqImportViewModel;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void RowDown_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is { HeaderRow: > 1 } vm) vm.HeaderRow--;
    }

    private void RowUp_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) vm.HeaderRow++;
    }

    private void AutoDetect_Click(object sender, RoutedEventArgs e) => Vm?.AutoDetectSheet();

    private void Import_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
