using System.Windows;
// Triggering rebuild
using DocumentManagement.ViewModels;

namespace DocumentManagement.Views;

public partial class UserProfileWindow : Window
{
    public UserProfileWindow(UserProfileViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        this.DialogResult = false;
        this.Close();
    }
}
