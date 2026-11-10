using System.Windows;

namespace DocumentManagement
{
    /// <summary>
    /// Splash screen hiển thị trong quá trình khởi động ứng dụng
    /// </summary>
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Cập nhật trạng thái loading
        /// </summary>
        /// <param name="status">Thông báo trạng thái</param>
        public void UpdateStatus(string status)
        {
            if (Dispatcher.CheckAccess())
            {
                StatusText.Text = status;
            }
            else
            {
                Dispatcher.Invoke(() => StatusText.Text = status);
            }
        }

        /// <summary>
        /// Cập nhật progress bar
        /// </summary>
        /// <param name="value">Giá trị từ 0-100</param>
        public void UpdateProgress(double value)
        {
            if (Dispatcher.CheckAccess())
            {
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = value;
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = value;
                });
            }
        }

        /// <summary>
        /// Ẩn splash screen (không đóng để giữ icon taskbar)
        /// </summary>
        public void CloseSplash()
        {
            if (Dispatcher.CheckAccess())
            {
                Hide(); // Ẩn thay vì đóng
            }
            else
            {
                Dispatcher.Invoke(Hide);
            }
        }
        
        /// <summary>
        /// Đóng splash screen hoàn toàn (gọi sau khi login window hiện)
        /// </summary>
        public void CloseSplashCompletely()
        {
            if (Dispatcher.CheckAccess())
            {
                Close();
            }
            else
            {
                Dispatcher.Invoke(Close);
            }
        }
    }
}