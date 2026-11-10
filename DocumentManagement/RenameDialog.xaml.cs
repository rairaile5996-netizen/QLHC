using System.Windows;
using System.Windows.Media;

namespace DocumentManagement
{
    public partial class RenameDialog : Window
    {
        public string NewName { get; private set; } = string.Empty;

        public RenameDialog(string currentName, string itemType, bool isDarkTheme = false)
        {
            InitializeComponent();
            TitleText.Text = $"Nhập tên mới cho {itemType}:";
            NameBox.Text = currentName;
            NameBox.SelectAll();
            NameBox.Focus();
            ApplyTheme(isDarkTheme);
        }

        private void ApplyTheme(bool isDark)
        {
            if (isDark)
            {
                MainBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
                MainBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 15, ShadowDepth = 0, Opacity = 0.3 };
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                NameBox.Background = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
                NameBox.Foreground = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                NameBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                NameBox.CaretBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                CancelBtn.Background = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                CancelBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
            }
            else
            {
                MainBorder.Background = Brushes.White;
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                MainBorder.Effect = null; // Remove shadow for light theme
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                NameBox.Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
                NameBox.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                NameBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                NameBox.CaretBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                CancelBtn.Background = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                CancelBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            NewName = NameBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
