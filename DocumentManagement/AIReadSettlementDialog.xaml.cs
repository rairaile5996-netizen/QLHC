using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DocumentManagement.Services;
using Microsoft.Win32;

namespace DocumentManagement
{
    public partial class AIReadSettlementDialog : Window
    {
        public AIService.SettlementExtractResult? Result { get; private set; }

        private readonly bool _isDark;
        private string? _selectedFilePath;
        private CancellationTokenSource? _aiCancellationTokenSource;

        public AIReadSettlementDialog(bool isDarkTheme = false)
        {
            InitializeComponent();
            _isDark = isDarkTheme;
            ApplyTheme(isDarkTheme);
        }

        private void ApplyTheme(bool isDark)
        {
            var borderColor = isDark
                ? new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55))
                : new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));

            if (isDark)
            {
                MainBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                MainBorder.BorderBrush = borderColor;
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                CloseIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                DropZoneTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                DropZoneSubtitle.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                CancelButton.Background = Brushes.Transparent;
                CancelButton.BorderBrush = borderColor;
                CancelButton.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                DropZone.BorderBrush = borderColor;
            }
            else
            {
                MainBorder.Background = Brushes.White;
                MainBorder.BorderBrush = borderColor;
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                CloseIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                DropZoneTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                DropZoneSubtitle.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                CancelButton.Background = Brushes.Transparent;
                CancelButton.BorderBrush = borderColor;
                CancelButton.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                DropZone.BorderBrush = borderColor;
            }
        }

        private void UpdateFileSelection(string? filePath)
        {
            _selectedFilePath = filePath;

            if (!string.IsNullOrEmpty(filePath))
            {
                var fileName = Path.GetFileName(filePath);
                DropZoneTitle.Text = fileName;
                DropZoneSubtitle.Text = "Bấm để chọn file khác";
                DropZoneIconDefault.Visibility = Visibility.Collapsed;
                DropZoneIconSelected.Visibility = Visibility.Visible;
                ReadAIButton.IsEnabled = true;
                DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            }
            else
            {
                DropZoneTitle.Text = "Kéo thả file vào hoặc bấm để chọn";
                DropZoneSubtitle.Text = "Hỗ trợ: .xlsx (file quyết toán)";
                DropZoneIconDefault.Visibility = Visibility.Visible;
                DropZoneIconSelected.Visibility = Visibility.Collapsed;
                ReadAIButton.IsEnabled = false;
                DropZone.BorderBrush = _isDark
                    ? new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55))
                    : new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
            }
        }

        #region Drop Zone Events
        private void DropZone_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Any(f => f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)))
                {
                    e.Effects = DragDropEffects.Copy;
                    DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    e.Handled = true;
                    return;
                }
            }
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void DropZone_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                DropZone.BorderBrush = _isDark
                    ? new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55))
                    : new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
            }
        }

        private void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var excelFile = files.FirstOrDefault(f => f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));
                if (excelFile != null)
                {
                    UpdateFileSelection(excelFile);
                }
            }
            e.Handled = true;
        }

        private bool _isFileDialogOpen = false;

        private void DropZone_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            // Prevent opening dialog multiple times
            if (_isFileDialogOpen) return;

            _isFileDialogOpen = true;

            // Use Dispatcher to open dialog after event processing completes
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var dialog = new OpenFileDialog
                    {
                        Filter = "Excel Files (*.xlsx)|*.xlsx",
                        Title = "Chọn file Excel quyết toán"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        UpdateFileSelection(dialog.FileName);
                    }
                }
                finally
                {
                    // Delay reset to prevent immediate re-trigger
                    Dispatcher.BeginInvoke(new Action(() => _isFileDialogOpen = false),
                        System.Windows.Threading.DispatcherPriority.Background);
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
        #endregion

        #region AI Processing
        private async void ReadAIButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath)) return;

            _aiCancellationTokenSource = new CancellationTokenSource();
            LoadingOverlay.Visibility = Visibility.Visible;
            ReadAIButton.IsEnabled = false;

            try
            {
                var aiService = new AIService();
                Result = await aiService.ExtractSettlementValueAsync(_selectedFilePath, _aiCancellationTokenSource.Token);

                if (_aiCancellationTokenSource.Token.IsCancellationRequested) return;

                if (Result != null)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("AI không đọc được dữ liệu từ file.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                ReadAIButton.IsEnabled = !string.IsNullOrEmpty(_selectedFilePath);
                _aiCancellationTokenSource?.Dispose();
                _aiCancellationTokenSource = null;
            }
        }

        private void CancelAI_Click(object sender, RoutedEventArgs e)
        {
            _aiCancellationTokenSource?.Cancel();
            LoadingOverlay.Visibility = Visibility.Collapsed;
            ReadAIButton.IsEnabled = !string.IsNullOrEmpty(_selectedFilePath);
        }
        #endregion

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
