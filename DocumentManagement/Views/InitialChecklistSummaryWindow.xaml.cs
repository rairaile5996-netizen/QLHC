using System.Windows;

namespace DocumentManagement.Views;

public partial class InitialChecklistSummaryWindow : Window
{
    public InitialChecklistSummaryWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
