using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentManagement.Models;
using DocumentManagement.Services;
using DocumentManagement.Utilities;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace DocumentManagement.ViewModels
{
    public partial class UserProfileViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly User _user;

        [ObservableProperty]
        private string _fullName;

        [ObservableProperty]
        private string _phone;

        [ObservableProperty]
        private string? _avatarPath;

        [ObservableProperty]
        private string _username;

        public UserProfileViewModel(IAuthService authService)
        {
            _authService = authService;
            _user = _authService.CurrentUser ?? new User();
            
            _username = _user.Username;
            _fullName = _user.FullName;
            _phone = _user.Phone;
            _avatarPath = _user.AvatarPath;
        }

        [RelayCommand]
        private void SelectAvatar()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    AvatarPath = AvatarStorage.CopyToAppData(openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể lưu ảnh đại diện: {ex.Message}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        [RelayCommand]
        private void Save(Window window)
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                MessageBox.Show("Vui lòng nhập họ và tên.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _authService.UpdateProfile(FullName, Phone, AvatarPath);
            window.DialogResult = true;
            window.Close();
        }

        [RelayCommand]
        private static void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
