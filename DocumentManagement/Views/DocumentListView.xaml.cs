using System.Windows;
using System.Windows.Controls;
using DocumentManagement.ViewModels;
using System.Diagnostics;
using System.Windows.Navigation;

namespace DocumentManagement.Views;

public partial class DocumentListView : System.Windows.Controls.UserControl
{
    public DocumentListView()
    {
        InitializeComponent();
    }

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        SortPopup.IsOpen = !SortPopup.IsOpen;
    }

    private void OpenContractorManager_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            var documentGeneratorService = new Services.DocumentGeneratorService(viewModel.GetAIService());
            var window = new ContractorManagerWindow(documentGeneratorService, viewModel.GetAIService())
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
