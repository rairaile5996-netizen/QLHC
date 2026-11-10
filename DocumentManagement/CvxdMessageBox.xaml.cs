using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace DocumentManagement
{
    public partial class CvxdMessageBox : Window
    {
        private MessageBoxResult _result = MessageBoxResult.None;

        public CvxdMessageBox()
        {
            InitializeComponent();
            Utilities.AppThemeResources.Apply(Resources, Utilities.AppThemeResources.LoadSavedIsDarkTheme());
        }

        public static MessageBoxResult Show(string title, string message, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information,
            string? yesText = null, string? noText = null, string? okText = null, string? cancelText = null)
        {
            var dialog = new CvxdMessageBox();
            dialog.TxtTitle.Text = title;
            dialog.TxtMessage.Text = message;
            if (yesText != null) dialog.BtnYes.Content = yesText;
            if (noText != null) dialog.BtnNo.Content = noText;
            if (okText != null) dialog.BtnOk.Content = okText;
            if (cancelText != null) dialog.BtnCancel.Content = cancelText;

            // Set Icon
            switch (icon)
            {
                case MessageBoxImage.Information:
                    dialog.MainIcon.Kind = PackIconKind.Information;
                    dialog.MainIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)); // Blue
                    break;
                case MessageBoxImage.Warning:
                    dialog.MainIcon.Kind = PackIconKind.Alert;
                    dialog.MainIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // Amber
                    break;
                case MessageBoxImage.Error:
                    dialog.MainIcon.Kind = PackIconKind.Error;
                    dialog.MainIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // Red
                    break;
                case MessageBoxImage.Question:
                    dialog.MainIcon.Kind = PackIconKind.HelpCircle;
                    dialog.MainIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)); // Blue
                    break;
            }

            // Set Buttons
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    dialog.BtnOk.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.OKCancel:
                    dialog.BtnOk.Visibility = Visibility.Visible;
                    dialog.BtnCancel.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                    dialog.BtnYes.Visibility = Visibility.Visible;
                    dialog.BtnNo.Visibility = Visibility.Visible;
                    dialog.BtnCancel.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    dialog.BtnYes.Visibility = Visibility.Visible;
                    dialog.BtnNo.Visibility = Visibility.Visible;
                    break;
            }

            if (Application.Current != null)
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win.IsActive || win.GetType().Name.Contains("CvxdWindow"))
                    {
                        dialog.Owner = win;
                        break;
                    }
                }
            }

            dialog.ShowDialog();
            return dialog._result;
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Yes;
            this.DialogResult = true;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.No;
            this.DialogResult = false;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.OK;
            this.DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Cancel;
            this.DialogResult = false;
            Close();
        }
    }
}
