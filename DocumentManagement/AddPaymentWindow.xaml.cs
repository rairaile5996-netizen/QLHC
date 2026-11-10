using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DocumentManagement.Models;
using DocumentManagement.Services;
using Microsoft.Win32;

namespace DocumentManagement
{
    public partial class AddPaymentWindow : Window
    {
        public enum PaymentType { Advance, Payment }

        public PaymentType Type { get; private set; }
        public AdvancePayment? Advance { get; private set; }
        public Payment? Payment { get; private set; }
        public bool IsEditMode { get; private set; }
        private bool _isDark;
        private int _currentTab = 0;
        private string? _selectedFilePath;
        private CancellationTokenSource? _aiCancellationTokenSource;
        private readonly ErrorHandlingService _errorHandlingService;
        public string? TargetAppendixNumber { get; set; }

        public AddPaymentWindow(PaymentType type, bool isDarkTheme = false, AdvancePayment? advanceToEdit = null, Payment? paymentToEdit = null)
        {
            InitializeComponent();
            Type = type;
            _isDark = isDarkTheme;
            _errorHandlingService = new ErrorHandlingService();

            IsEditMode = advanceToEdit != null || paymentToEdit != null;

            if (type == PaymentType.Advance)
            {
                if (IsEditMode && advanceToEdit != null)
                {
                    TitleText.Text = "Chỉnh Sửa Tạm Ứng";
                    SaveButton.Content = "Lưu Thay Đổi";
                    PaymentDatePicker.SelectedDate = advanceToEdit.PaymentDate;
                    DescriptionBox.Text = advanceToEdit.Description;
                    ValueBox.Text = advanceToEdit.Value.ToString("N0");
                    Advance = advanceToEdit;
                    SwitchToTab(0); // Manual tab for editing
                }
                else
                {
                    TitleText.Text = "Thêm Tạm Ứng";
                    SaveButton.Content = "Thêm Tạm Ứng";
                    PaymentDatePicker.SelectedDate = DateTime.Today;
                    SwitchToTab(0);
                }
            }
            else
            {
                if (IsEditMode && paymentToEdit != null)
                {
                    TitleText.Text = "Chỉnh Sửa Thanh Toán";
                    SaveButton.Content = "Lưu Thay Đổi";
                    PaymentDatePicker.SelectedDate = paymentToEdit.PaymentDate;
                    DescriptionBox.Text = paymentToEdit.Description;
                    ValueBox.Text = paymentToEdit.Value.ToString("N0");
                    Payment = paymentToEdit;
                    SwitchToTab(0); // Manual tab for editing
                }
                else
                {
                    TitleText.Text = "Thêm Thanh Toán";
                    SaveButton.Content = "Thêm Thanh Toán";
                    PaymentDatePicker.SelectedDate = DateTime.Today;
                    SwitchToTab(0);
                }
            }

            ApplyTheme(isDarkTheme);
        }

        private void SwitchToTab(int tabIndex)
        {
            _currentTab = tabIndex;
            var activeColor = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            var inactiveColor = _isDark
                ? new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
                : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

            if (tabIndex == 0) // Manual tab (default)
            {
                ManualTabContent.Visibility = Visibility.Visible;
                AITabContent.Visibility = Visibility.Collapsed;
                SaveButton.Visibility = Visibility.Visible;

                TabManual.BorderBrush = activeColor;
                TabManualIcon.Foreground = activeColor;
                TabManualText.Foreground = activeColor;

                TabAI.BorderBrush = Brushes.Transparent;
                TabAIIcon.Foreground = inactiveColor;
                TabAIText.Foreground = inactiveColor;
            }
            else // AI tab
            {
                AITabContent.Visibility = Visibility.Visible;
                ManualTabContent.Visibility = Visibility.Collapsed;
                SaveButton.Visibility = Visibility.Collapsed;

                TabAI.BorderBrush = activeColor;
                TabAIIcon.Foreground = activeColor;
                TabAIText.Foreground = activeColor;

                TabManual.BorderBrush = Brushes.Transparent;
                TabManualIcon.Foreground = inactiveColor;
                TabManualText.Foreground = inactiveColor;
            }
        }

        private void TabManual_Click(object sender, RoutedEventArgs e) => SwitchToTab(0);
        private void TabAI_Click(object sender, RoutedEventArgs e) => SwitchToTab(1);

        private void ApplyTheme(bool isDark)
        {
            _isDark = isDark;
            var borderColor = isDark
                ? new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55))
                : new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));

            if (isDark)
            {
                MainBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                MainBorder.BorderBrush = borderColor;
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                CloseIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

                var labelColor = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                LblDate.Foreground = labelColor;
                LblDesc.Foreground = labelColor;
                LblValue.Foreground = labelColor;
                DropZoneTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                DropZoneSubtitle.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

                var inputBg = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
                var inputFg = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));

                PaymentDatePicker.Background = inputBg;
                PaymentDatePicker.BorderBrush = borderColor;
                PaymentDatePicker.Foreground = inputFg;
                DescriptionBox.Background = inputBg;
                DescriptionBox.BorderBrush = borderColor;
                DescriptionBox.Foreground = inputFg;
                ValueBox.Background = inputBg;
                ValueBox.BorderBrush = borderColor;
                ValueBox.Foreground = inputFg;

                // Cancel button - có viền
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

                var labelColor = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                LblDate.Foreground = labelColor;
                LblDesc.Foreground = labelColor;
                LblValue.Foreground = labelColor;
                DropZoneTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                DropZoneSubtitle.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));

                var inputBg = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
                var inputFg = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));

                PaymentDatePicker.Background = inputBg;
                PaymentDatePicker.BorderBrush = borderColor;
                PaymentDatePicker.Foreground = inputFg;
                DescriptionBox.Background = inputBg;
                DescriptionBox.BorderBrush = borderColor;
                DescriptionBox.Foreground = inputFg;
                ValueBox.Background = inputBg;
                ValueBox.BorderBrush = borderColor;
                ValueBox.Foreground = inputFg;

                // Cancel button - có viền
                CancelButton.Background = Brushes.Transparent;
                CancelButton.BorderBrush = borderColor;
                CancelButton.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));

                DropZone.BorderBrush = borderColor;
            }

            SwitchToTab(_currentTab);
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
                DropZoneSubtitle.Text = "Hỗ trợ: .xlsx";
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
            ResetDropZoneStyle();
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

        private void ResetDropZoneStyle()
        {
            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                DropZone.BorderBrush = _isDark
                    ? new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55))
                    : new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
            }
        }

        private bool _isFileDialogOpen = false;

        private void DropZone_Click(object sender, MouseButtonEventArgs e)
        {
            // Prevent double trigger
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
                        Title = "Chọn file Excel để đọc giá trị"
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
        private void ReadAIButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath)) return;
            ProcessFileWithAI(_selectedFilePath);
        }

        private async void ProcessFileWithAI(string filePath)
        {
            _aiCancellationTokenSource = new CancellationTokenSource();
            LoadingOverlay.Visibility = Visibility.Visible;
            ReadAIButton.IsEnabled = false;

            try
            {
                var AIService = new AIService();
                AIService.AdvanceExtractResult? result;
                string defaultDesc;
                string notFoundMsg;

                if (Type == PaymentType.Advance)
                {
                    result = await AIService.ExtractAdvanceValueAsync(filePath, _aiCancellationTokenSource.Token);
                    defaultDesc = "Tạm ứng";
                    notFoundMsg = "AI không tìm thấy giá trị tạm ứng trong file.";
                }
                else
                {
                    result = await AIService.ExtractPaymentValueAsync(filePath, _aiCancellationTokenSource.Token);
                    defaultDesc = "Thanh toán";
                    notFoundMsg = "AI không tìm thấy giá trị thanh toán trong file.";
                }

                if (_aiCancellationTokenSource.Token.IsCancellationRequested) return;

                if (result != null && result.Value > 0)
                {
                    ValueBox.Text = result.Value.ToString("N0");
                    DescriptionBox.Text = !string.IsNullOrEmpty(result.Description)
                        ? result.Description
                        : defaultDesc;
                    TargetAppendixNumber = result.TargetAppendixNumber;
                    AIWarningBanner.Visibility = Visibility.Visible;
                    SwitchToTab(1);
                }
                else
                {
                    _errorHandlingService.ShowWarning(notFoundMsg);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleError(ex, "AI đọc thanh toán");
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

        #region Form Events
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!PaymentDatePicker.SelectedDate.HasValue)
            {
                _errorHandlingService.ShowWarning("Vui lòng chọn ngày.");
                return;
            }

            if (!decimal.TryParse(ValueBox.Text.Replace(",", "").Replace(".", ""), out decimal value) || value <= 0)
            {
                _errorHandlingService.ShowWarning("Vui lòng nhập giá trị hợp lệ (số dương).");
                return;
            }

            if (Type == PaymentType.Advance)
            {
                if (IsEditMode && Advance != null)
                {
                    Advance.PaymentDate = PaymentDatePicker.SelectedDate.Value;
                    Advance.Description = DescriptionBox.Text;
                    Advance.Value = value;
                }
                else
                {
                    Advance = new AdvancePayment
                    {
                        PaymentDate = PaymentDatePicker.SelectedDate.Value,
                        Description = DescriptionBox.Text,
                        Value = value
                    };
                }
            }
            else
            {
                if (IsEditMode && Payment != null)
                {
                    Payment.PaymentDate = PaymentDatePicker.SelectedDate.Value;
                    Payment.Description = DescriptionBox.Text;
                    Payment.Value = value;
                }
                else
                {
                    Payment = new Payment
                    {
                        PaymentDate = PaymentDatePicker.SelectedDate.Value,
                        Description = DescriptionBox.Text,
                        Value = value
                    };
                }
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ValueBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (textBox.Text == "0")
                    textBox.Text = "";
                textBox.SelectAll();
            }
        }

        private void ValueBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                    textBox.Text = "0";
            }
        }
        #endregion
    }
}
