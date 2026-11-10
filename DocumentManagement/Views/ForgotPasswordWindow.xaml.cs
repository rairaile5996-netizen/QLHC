using System.Windows;
using DocumentManagement.Services;

namespace DocumentManagement.Views;

public partial class ForgotPasswordWindow : Window
{
    private readonly IAuthService _authService;

    public ForgotPasswordWindow(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text.Trim();
        var phone = PhoneBox.Text.Trim();
        var password = NewPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(phone) || password.Length < 6)
        {
            MessageBox.Show("Vui lòng nhập đủ thông tin. Mật khẩu mới phải có ít nhất 6 ký tự.", "Khôi phục mật khẩu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (password != ConfirmPasswordBox.Password)
        {
            MessageBox.Show("Hai mật khẩu mới không trùng nhau.", "Khôi phục mật khẩu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_authService.ResetPassword(username, phone, password))
        {
            MessageBox.Show("Tên đăng nhập hoặc số điện thoại không đúng.", "Khôi phục mật khẩu", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show("Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới.", "Khôi phục mật khẩu", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
}
