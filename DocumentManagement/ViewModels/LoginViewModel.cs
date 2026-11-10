using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentManagement.Services;
using DocumentManagement.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.IO;
using DocumentManagement.Views;

namespace DocumentManagement.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private string _username = "";

        [ObservableProperty]
        private string _password = "";

        [ObservableProperty]
        private string _fullName = "";

        [ObservableProperty]
        private string _phone = "";

        [ObservableProperty]
        private string? _avatarPath;

        [ObservableProperty]
        private bool _isRegistrationMode;

        [ObservableProperty]
        private string _errorMessage = "";

        [ObservableProperty]
        private bool _isPasswordVisible;
        
        [ObservableProperty]
        private bool _isQuickLoginMode;
        
        [ObservableProperty]
        private bool _isLoggingIn;

        [ObservableProperty]
        private string _lastUserFullName = "";

        [ObservableProperty]
        private string? _lastUserAvatarPath;

        [RelayCommand]
        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }

        [RelayCommand]
        private void ForgotPassword()
        {
            var window = new ForgotPasswordWindow(_authService)
            {
                Owner = Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault()
            };
            window.ShowDialog();
        }

        public LoginViewModel(IAuthService authService, IServiceProvider serviceProvider)
        {
            _authService = authService;
            _serviceProvider = serviceProvider;
            IsRegistrationMode = false;
        }

        public async Task InitializeAsync()
        {
            try
            {
                await App.DatabaseReadyTask;
                IsRegistrationMode = !_authService.HasAdminAccount();

                if (!IsRegistrationMode)
                {
                    var lastUser = _authService.GetLastUser();
                    if (lastUser != null)
                    {
                        IsQuickLoginMode = true;
                        Username = lastUser.Value.Username;
                        LastUserFullName = lastUser.Value.FullName;
                        LastUserAvatarPath = lastUser.Value.AvatarPath;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Không thể khởi tạo cơ sở dữ liệu: {ex.Message}";
            }
        }

        [RelayCommand]
        private void SwitchToFullLogin()
        {
            IsQuickLoginMode = false;
            _authService.ClearLastUser(); // Clear to not show it again next time unless logged in
            Username = "";
            Password = "";
        }

        [RelayCommand]
        private async Task Login()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.";
                return;
            }

            IsLoggingIn = true;
            ErrorMessage = "";
            
            try
            {
                // Simulate minimum loading time để user thấy feedback
                await Task.Delay(500);
                
                if (_authService.Login(Username, Password))
                {
                    await OpenMainWindowAsync();
                }
                else
                {
                    ErrorMessage = "Tên đăng nhập hoặc mật khẩu không chính xác.";
                }
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        [RelayCommand]
        private async Task LoginAsGuest()
        {
            IsLoggingIn = true;
            ErrorMessage = "";
            
            try
            {
                await Task.Delay(300);
                _authService.LoginAsGuest();
                await OpenMainWindowAsync();
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        [RelayCommand]
        private async Task RegisterAdmin()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || 
                string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Phone))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ thông tin đăng ký.";
                return;
            }

            IsLoggingIn = true;
            ErrorMessage = "";
            
            try
            {
                await Task.Delay(500);
                
                if (_authService.CreateAdmin(Username, Password, FullName, Phone, AvatarPath))
                {
                    if (_authService.Login(Username, Password))
                    {
                        await OpenMainWindowAsync();
                    }
                    else
                    {
                        ErrorMessage = "Đăng ký thành công nhưng không thể đăng nhập tự động. Vui lòng thử lại.";
                    }
                }
                else
                {
                    ErrorMessage = "Không thể tạo tài khoản Admin. Tên đăng nhập có thể đã tồn tại.";
                }
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        [RelayCommand]
        private void SelectAvatar()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Chọn ảnh đại diện"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    AvatarPath = AvatarStorage.CopyToAppData(openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Không thể lưu ảnh đại diện: {ex.Message}";
                }
            }
        }

        private async Task OpenMainWindowAsync()
        {
            // Launch module selection window
            var moduleSelectionWindow = _serviceProvider.GetRequiredService<ModuleSelectionWindow>();
            
            // Đổi MainWindow để WPF không tự tắt app khi LoginWindow đóng
            Application.Current.MainWindow = moduleSelectionWindow;
            
            // Hiển thị ModuleSelectionWindow trước
            moduleSelectionWindow.WindowState = WindowState.Normal;
            moduleSelectionWindow.Visibility = Visibility.Visible;
            moduleSelectionWindow.ShowInTaskbar = true;
            moduleSelectionWindow.Show();
            moduleSelectionWindow.Activate();
            
            // Đợi một chút để window render
            await Task.Delay(100);
            
            // Close login window
            Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault()?.Close();
        }
    }
}
