using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace DocumentManagement.Models
{
    public enum UserRole
    {
        Admin,
        Guest
    }

    public class User : INotifyPropertyChanged
    {
        private string _fullName = string.Empty;
        private string _phone = string.Empty;
        private string? _avatarPath;

        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(100)]
        public string FullName 
        { 
            get => _fullName; 
            set { _fullName = value; OnPropertyChanged(); } 
        }

        [MaxLength(20)]
        public string Phone 
        { 
            get => _phone; 
            set { _phone = value; OnPropertyChanged(); } 
        }

        public string? AvatarPath 
        { 
            get => _avatarPath; 
            set { _avatarPath = value; OnPropertyChanged(); } 
        }

        public UserRole Role { get; set; } = UserRole.Guest;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
