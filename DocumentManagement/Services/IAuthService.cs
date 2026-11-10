using DocumentManagement.Models;

namespace DocumentManagement.Services
{
    public interface IAuthService
    {
        User? CurrentUser { get; }
        UserRole CurrentRole { get; }
        bool IsAdmin { get; }
        bool IsGuest { get; }
        bool IsLoggedIn { get; }

        bool Login(string username, string password);
        void LoginAsGuest();
        void Logout();
        void Lock();
        
        bool HasAdminAccount();
        bool CreateAdmin(string username, string password, string fullName, string phone, string? avatarPath);
        bool ResetPassword(string username, string phone, string newPassword);
        void UpdateProfile(string fullName, string phone, string? avatarPath);

        // Quick Login support
        (string Username, string FullName, string? AvatarPath)? GetLastUser();
        void ClearLastUser();
    }
}
