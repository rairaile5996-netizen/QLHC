using System.Windows;
using System.Windows.Media;

namespace DocumentManagement
{
    public partial class ConfirmDialog : Window
    {
        public bool IsConfirmed { get; private set; }
        private static bool _isDarkTheme = true;

        public ConfirmDialog(string title, string message, string? detail = null, string confirmText = "Xác nhận", string cancelText = "Hủy")
        {
            InitializeComponent();

            TxtTitle.Text = title;
            TxtMessage.Text = message;
            BtnConfirm.Content = confirmText;
            BtnCancel.Content = cancelText;

            if (!string.IsNullOrEmpty(detail))
            {
                TxtDetail.Text = detail;
                TxtDetail.Visibility = Visibility.Visible;
            }

            ApplyTheme(_isDarkTheme);
        }

        private void ApplyTheme(bool isDark)
        {
            if (isDark)
            {
                MainBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
                IconBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                TxtTitle.Foreground = Brushes.White;
                TxtMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                TxtDetail.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                FooterBorder.Background = Brushes.Transparent;
                BtnCancel.Foreground = Brushes.White;
                BtnCancel.Background = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                BtnConfirm.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
            }
            else
            {
                MainBorder.Background = Brushes.White;
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xBA, 0xC7, 0xD6));
                IconBorder.Background = new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE));
                TxtTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x17, 0x20, 0x33));
                TxtMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                TxtDetail.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                FooterBorder.Background = Brushes.Transparent;
                BtnCancel.Foreground = new SolidColorBrush(Color.FromRgb(0x17, 0x20, 0x33));
                BtnCancel.Background = new SolidColorBrush(Color.FromRgb(0xE1, 0xE8, 0xF0));
                BtnConfirm.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }

        public static void SetTheme(bool isDark)
        {
            _isDarkTheme = isDark;
        }

        public static bool Show(string title, string message, string? detail = null, string confirmText = "Xác nhận", string cancelText = "Hủy")
        {
            var dialog = new ConfirmDialog(title, message, detail, confirmText, cancelText)
            {
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current.MainWindow
            };
            return dialog.ShowDialog() == true;
        }
    }
}
