using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DocumentManagement.Models;
using DocumentManagement.Services;
using Microsoft.Win32;
using MaterialDesignThemes.Wpf;

namespace DocumentManagement
{
    public partial class AddEditAppendixWindow : Window
    {
        public ContractAppendix? Appendix { get; private set; }
        public List<string> UploadedFilePaths { get; private set; } = [];
        private readonly bool _isEditMode;
        private readonly int _appendixId;
        private readonly AIService _AIService = new();
        private readonly bool _isDarkTheme;
        private readonly ObservableCollection<AppendixFileItem> _selectedFiles = [];
        private bool _isAIMode = false;
        private CancellationTokenSource? _aiCancellationTokenSource;
        private readonly ErrorHandlingService _errorHandlingService;

        public AddEditAppendixWindow(bool isDarkTheme = false)
        {
            InitializeComponent();
            _isDarkTheme = isDarkTheme;
            _isEditMode = false;
            _errorHandlingService = new ErrorHandlingService();
            SignDatePicker.SelectedDate = DateTime.Today;
            FileList.ItemsSource = _selectedFiles;
            ApplyTheme(isDarkTheme);
            UpdateTabStyles();
        }

        public AddEditAppendixWindow(ContractAppendix appendix, bool isDarkTheme = false) : this(isDarkTheme)
        {
            _isEditMode = true;
            _appendixId = appendix.Id;

            AppendixNumberBox.Text = appendix.AppendixNumber;
            DescriptionBox.Text = appendix.Description;
            SignDatePicker.SelectedDate = appendix.SignDate;
            ValueBox.Text = appendix.Value.ToString("N0");
            PlannedAdvanceValueBox.Text = appendix.PlannedAdvanceValue.ToString("N0");

            TitleText.Text = "Chỉnh Sửa Phụ Lục";
            SaveButton.Content = "Cập Nhật";

            // Ẩn tab AI khi edit
            TabAI.Visibility = Visibility.Collapsed;
        }

        private void TabManual_Click(object sender, RoutedEventArgs e)
        {
            _isAIMode = false;
            ManualContent.Visibility = Visibility.Visible;
            AIContent.Visibility = Visibility.Collapsed;
            UpdateTabStyles();
        }

        private void TabAI_Click(object sender, RoutedEventArgs e)
        {
            _isAIMode = true;
            ManualContent.Visibility = Visibility.Collapsed;
            AIContent.Visibility = Visibility.Visible;
            UpdateTabStyles();
        }

        private void UpdateTabStyles()
        {
            var activeBg = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(0x1E, 0x29, 0x3B) : Colors.White);
            var inactiveBg = Brushes.Transparent;
            var activeFg = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(0xF1, 0xF5, 0xF9) : Color.FromRgb(0x1E, 0x29, 0x3B));
            var inactiveFg = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));

            TabManual.Background = _isAIMode ? inactiveBg : activeBg;
            TabManual.Foreground = _isAIMode ? inactiveFg : activeFg;
            TabAI.Background = _isAIMode ? activeBg : inactiveBg;
            TabAI.Foreground = _isAIMode ? activeFg : inactiveFg;
        }

        private bool _isFileDialogOpen = false;

        private void DropZone_Click(object sender, MouseButtonEventArgs e)
        {
            if (_isFileDialogOpen) return;
            _isFileDialogOpen = true;
            e.Handled = true;

            // Delay để tránh double-trigger
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    OpenFileDialog();
                }
                finally
                {
                    _isFileDialogOpen = false;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void DropZone_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFiles(files);
            }
        }

        private void OpenFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Word/Excel Files (*.docx;*.xlsx)|*.docx;*.xlsx",
                Title = "Chọn file phụ lục",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                AddFiles(dialog.FileNames);
            }
        }

        private void AddFiles(string[] files)
        {
            var validExtensions = new[] { ".docx", ".xlsx" };
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLower();
                if (!validExtensions.Contains(ext)) continue;
                if (UploadedFilePaths.Contains(file)) continue;

                _selectedFiles.Add(new AppendixFileItem
                {
                    FileName = Path.GetFileName(file),
                    FilePath = file
                });
                UploadedFilePaths.Add(file);
            }
            BtnReadAI.IsEnabled = _selectedFiles.Count > 0;
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string filePath)
            {
                var item = _selectedFiles.FirstOrDefault(f => f.FilePath == filePath);
                if (item != null)
                {
                    _selectedFiles.Remove(item);
                    UploadedFilePaths.Remove(filePath);
                }
                BtnReadAI.IsEnabled = _selectedFiles.Count > 0;
            }
        }

        private async void BtnReadAI_Click(object sender, RoutedEventArgs e)
        {
            if (UploadedFilePaths.Count == 0) return;

            if (_AIService.GetApiKeyCount() == 0)
            {
                AIStatusText.Text = "⚠️ Chưa cấu hình API Key. Vào Cài đặt để thêm.";
                AIStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                AIStatusText.Visibility = Visibility.Visible;
                return;
            }

            // Show loading overlay
            _aiCancellationTokenSource = new CancellationTokenSource();
            LoadingOverlay.Visibility = Visibility.Visible;
            BtnReadAI.IsEnabled = false;
            AIStatusText.Visibility = Visibility.Collapsed;

            try
            {
                var result = await _AIService.ExtractAppendixInfoAsync(UploadedFilePaths, _aiCancellationTokenSource.Token);

                if (_aiCancellationTokenSource.Token.IsCancellationRequested) return;

                if (result != null)
                {
                    if (!string.IsNullOrEmpty(result.AppendixNumber))
                        AppendixNumberBox.Text = result.AppendixNumber;
                    if (!string.IsNullOrEmpty(result.Description))
                        DescriptionBox.Text = result.Description;
                    if (result.SignDate.HasValue)
                        SignDatePicker.SelectedDate = result.SignDate.Value;
                    if (result.Value.HasValue)
                        ValueBox.Text = result.Value.Value.ToString("N0");
                    if (result.PlannedAdvanceValue.HasValue)
                        PlannedAdvanceValueBox.Text = result.PlannedAdvanceValue.Value.ToString("N0");

                    // Hide loading overlay
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    BtnReadAI.IsEnabled = true;

                    // Chuyển sang tab Manual
                    await System.Threading.Tasks.Task.Delay(300);
                    TabManual_Click(null!, null!);
                    return;
                }
                else
                {
                    AIStatusText.Text = "⚠️ Không thể trích xuất thông tin từ file.";
                    AIStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    AIStatusText.Visibility = Visibility.Visible;
                }
            }
            catch (OperationCanceledException)
            {
                AIStatusText.Text = "⚠️ Đã hủy đọc file.";
                AIStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                AIStatusText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                AIStatusText.Text = $"❌ Lỗi: {ex.Message}";
                AIStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                AIStatusText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                BtnReadAI.IsEnabled = true;
                _aiCancellationTokenSource?.Dispose();
                _aiCancellationTokenSource = null;
            }
        }

        private void CancelAI_Click(object sender, RoutedEventArgs e)
        {
            _aiCancellationTokenSource?.Cancel();
            LoadingOverlay.Visibility = Visibility.Collapsed;
            BtnReadAI.IsEnabled = true;
        }

        private void ApplyTheme(bool isDark)
        {
            if (isDark)
            {
                MainBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                CloseIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

                TabBorder.Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));

                var labelColor = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                LblAppendixNumber.Foreground = labelColor;
                LblSignDate.Foreground = labelColor;
                LblDesc.Foreground = labelColor;
                LblValue.Foreground = labelColor;
                LblHint.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                LblSelectFile.Foreground = labelColor;
                DropZoneText.Foreground = labelColor;
                DropZoneHint.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));

                var inputBg = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
                var inputBorder = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
                var inputFg = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));

                AppendixNumberBox.Background = inputBg;
                AppendixNumberBox.BorderBrush = inputBorder;
                AppendixNumberBox.Foreground = inputFg;

                SignDatePicker.Background = inputBg;
                SignDatePicker.BorderBrush = inputBorder;
                SignDatePicker.Foreground = inputFg;

                DescriptionBox.Background = inputBg;
                DescriptionBox.BorderBrush = inputBorder;
                DescriptionBox.Foreground = inputFg;

                ValueBox.Background = inputBg;
                ValueBox.BorderBrush = inputBorder;
                ValueBox.Foreground = inputFg;

                PlannedAdvanceValueBox.Background = inputBg;
                PlannedAdvanceValueBox.BorderBrush = inputBorder;
                PlannedAdvanceValueBox.Foreground = inputFg;

                CancelButton.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

                DropZone.Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
            }
            else
            {
                MainBorder.Background = Brushes.White;
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                CloseIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));

                TabBorder.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));

                var labelColor = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                LblAppendixNumber.Foreground = labelColor;
                LblSignDate.Foreground = labelColor;
                LblDesc.Foreground = labelColor;
                LblValue.Foreground = labelColor;
                LblHint.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                LblSelectFile.Foreground = labelColor;
                DropZoneText.Foreground = labelColor;
                DropZoneHint.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));

                var inputBg = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
                var inputBorder = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
                var inputFg = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));

                AppendixNumberBox.Background = inputBg;
                AppendixNumberBox.BorderBrush = inputBorder;
                AppendixNumberBox.Foreground = inputFg;

                SignDatePicker.Background = inputBg;
                SignDatePicker.BorderBrush = inputBorder;
                SignDatePicker.Foreground = inputFg;

                DescriptionBox.Background = inputBg;
                DescriptionBox.BorderBrush = inputBorder;
                DescriptionBox.Foreground = inputFg;

                ValueBox.Background = inputBg;
                ValueBox.BorderBrush = inputBorder;
                ValueBox.Foreground = inputFg;

                PlannedAdvanceValueBox.Background = inputBg;
                PlannedAdvanceValueBox.BorderBrush = inputBorder;
                PlannedAdvanceValueBox.Foreground = inputFg;

                CancelButton.Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));

                DropZone.Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AppendixNumberBox.Text))
            {
                _errorHandlingService.ShowWarning("Vui lòng nhập số phụ lục.");
                return;
            }

            if (!TryParseSignedCurrency(ValueBox.Text, out decimal value))
            {
                _errorHandlingService.ShowWarning("Giá trị phụ lục không hợp lệ.");
                return;
            }

            Appendix = new ContractAppendix
            {
                Id = _isEditMode ? _appendixId : 0,
                AppendixNumber = AppendixNumberBox.Text.Trim(),
                Description = DescriptionBox.Text?.Trim() ?? string.Empty,
                SignDate = SignDatePicker.SelectedDate ?? DateTime.Today,
                Value = value,
                PlannedAdvanceValue = TryParseSignedCurrency(PlannedAdvanceValueBox.Text, out decimal adv) ? adv : 0
            };

            DialogResult = true;
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

        private void SignedNumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox textBox) return;

            var candidate = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
                .Insert(textBox.SelectionStart, e.Text);

            // A leading minus is valid for appendix adjustments. Thousands
            // separators remain accepted and are normalized before saving.
            e.Handled = !Regex.IsMatch(candidate, @"^-?[0-9.,\s]*$");
        }

        private static bool TryParseSignedCurrency(string? text, out decimal value)
        {
            var normalized = (text ?? string.Empty)
                .Trim()
                .Replace("−", "-")
                .Replace(".", string.Empty)
                .Replace(",", string.Empty)
                .Replace(" ", string.Empty);

            return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        private void ValueBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                    textBox.Text = "0";
            }
        }
    }
}
