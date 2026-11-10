using System.Security.Cryptography;
using System.Text;
using DocumentManagement.Data;
using DocumentManagement.Models;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Text.Json;

namespace DocumentManagement.Services
{
    public class AuthService(IServiceScopeFactory scopeFactory) : IAuthService
    {
        private User? _currentUser;
        private UserRole _currentRole = UserRole.Guest;
        private bool _isLoggedIn = false;
        private readonly string _lastUserFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement", "last_user.json");

        public User? CurrentUser => _currentUser;
        public UserRole CurrentRole => _currentRole;
        public bool IsAdmin => _isLoggedIn && _currentRole == UserRole.Admin;
        public bool IsGuest => _isLoggedIn && _currentRole == UserRole.Guest;
        public bool IsLoggedIn => _isLoggedIn;

        public bool HasAdminAccount()
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return db.Users.Any(u => u.Role == UserRole.Admin);
        }

        public bool CreateAdmin(string username, string password, string fullName, string phone, string? avatarPath)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            if (db.Users.Any(u => u.Username == username)) return false;

            var user = new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                FullName = fullName,
                Phone = phone,
                AvatarPath = avatarPath,
                Role = UserRole.Admin
            };

            db.Users.Add(user);
            db.SaveChanges();
            return true;
        }

        public bool Login(string username, string password)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = db.Users.FirstOrDefault(u => u.Username == username);

            if (user != null && VerifyPassword(password, user.PasswordHash))
            {
                if (NeedsPasswordRehash(user.PasswordHash))
                {
                    user.PasswordHash = HashPassword(password);
                    db.SaveChanges();
                }

                _currentUser = user;
                _currentRole = user.Role;
                _isLoggedIn = true;
                
                SaveLastUser(user);
                return true;
            }
            return false;
        }

        public bool ResetPassword(string username, string phone, string newPassword)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = db.Users.FirstOrDefault(u => u.Username == username && u.Phone == phone);
            if (user == null) return false;

            user.PasswordHash = HashPassword(newPassword);
            db.SaveChanges();
            return true;
        }

        private void SaveLastUser(User user)
        {
            try
            {
                var directory = Path.GetDirectoryName(_lastUserFilePath);
                if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

                var info = new LastUserInfo { Username = user.Username, FullName = user.FullName, AvatarPath = user.AvatarPath };
                File.WriteAllText(_lastUserFilePath, JsonSerializer.Serialize(info));
            }
            catch { }
        }

        public (string Username, string FullName, string? AvatarPath)? GetLastUser()
        {
            try
            {
                if (File.Exists(_lastUserFilePath))
                {
                    var json = File.ReadAllText(_lastUserFilePath);
                    var info = JsonSerializer.Deserialize<LastUserInfo>(json);
                    if (info != null) return (info.Username, info.FullName, info.AvatarPath);
                }
            }
            catch { }
            return null;
        }

        public void ClearLastUser()
        {
            try
            {
                if (File.Exists(_lastUserFilePath)) File.Delete(_lastUserFilePath);
            }
            catch { }
        }

        public void LoginAsGuest()
        {
            _currentUser = null;
            _currentRole = UserRole.Guest;
            _isLoggedIn = true;
        }

        public void Logout()
        {
            _currentUser = null;
            _currentRole = UserRole.Guest;
            _isLoggedIn = false;
            ClearLastUser();
        }

        public void Lock()
        {
            // End the active session but keep the last username for quick re-login.
            _currentUser = null;
            _currentRole = UserRole.Guest;
            _isLoggedIn = false;
        }

        public void UpdateProfile(string fullName, string phone, string? avatarPath)
        {
            if (_currentUser == null) return;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var user = db.Users.Find(_currentUser.Id);
            if (user != null)
            {
                user.FullName = fullName;
                user.Phone = phone;
                user.AvatarPath = avatarPath;
                db.SaveChanges();
                
                // Sync local state
                _currentUser.FullName = fullName;
                _currentUser.Phone = phone;
                _currentUser.AvatarPath = avatarPath;
            }
        }

        private static string HashPassword(string password)
        {
            const int iterations = 100_000;
            const int saltSize = 16;
            const int keySize = 32;

            var salt = RandomNumberGenerator.GetBytes(saltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                keySize);

            return $"PBKDF2-SHA256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            if (storedHash.StartsWith("PBKDF2-SHA256$", StringComparison.Ordinal))
            {
                var parts = storedHash.Split('$');
                if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations)) return false;

                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(password),
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expected.Length);

                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }

            var legacyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(legacyHash),
                Encoding.UTF8.GetBytes(storedHash));
        }

        private static bool NeedsPasswordRehash(string storedHash)
        {
            return !storedHash.StartsWith("PBKDF2-SHA256$", StringComparison.Ordinal);
        }

        private class LastUserInfo
        {
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string? AvatarPath { get; set; }
        }
    }
}
