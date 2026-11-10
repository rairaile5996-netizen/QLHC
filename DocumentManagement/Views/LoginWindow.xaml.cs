using System.Windows;
using System.Windows.Input;
using DocumentManagement.ViewModels;

namespace DocumentManagement.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            await viewModel.InitializeAsync();
            FocusLoginInput();
        };
        viewModel.PropertyChanged += LoginViewModel_PropertyChanged;
        Closed += (_, _) => viewModel.PropertyChanged -= LoginViewModel_PropertyChanged;
    }

    private void LoginViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (IsLoaded && e.PropertyName == nameof(LoginViewModel.IsQuickLoginMode))
            Dispatcher.BeginInvoke(new System.Action(FocusLoginInput));
    }

    private void FocusLoginInput()
    {
        if (DataContext is not LoginViewModel vm) return;
        if (vm.IsQuickLoginMode)
            QuickPassBox.Focus();
        else
            UsernameBox.Focus();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void DragWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void PassBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = ((System.Windows.Controls.PasswordBox)sender).Password;
        }
    }

    private void QuickPassBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = ((System.Windows.Controls.PasswordBox)sender).Password;
        }
    }
}
