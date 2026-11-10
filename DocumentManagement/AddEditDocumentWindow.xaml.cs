using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentManagement.Models;
using DocumentManagement.Services;
using DocumentManagement.Utilities;
using Microsoft.Win32;
using MaterialDesignThemes.Wpf;

namespace DocumentManagement
{
    public partial class AddEditDocumentWindow : Window
    {
        private readonly string _baseFolderPath = @"D:\HỒ SƠ";
        private readonly ErrorHandlingService _errorHandlingService;
        private readonly bool _isDarkTheme;
        private readonly AIService _aiService;
        private readonly ObservableCollection<AIFileItem> _selectedAIFiles = [];
        private CancellationTokenSource? _aiCancellationTokenSource;

        public Document? Document { get; private set; }
        public List<Document>? ImportedDocuments { get; private set; }
        public List<string> UploadedFilePaths { get; private set; } = [];
        public bool IsEditMode { get; }
        public bool IsImportMode { get; private set; }
        public bool IsAIMode { get; private set; } = false;

        public AddEditDocumentWindow(Document? document = null, string? baseFolderPath = null, bool isDarkTheme = true, AIService? aiService = null)
        {
            InitializeComponent();
            _isDarkTheme = isDarkTheme;
            _aiService = aiService ?? new AIService();
            _errorHandlingService = new ErrorHandlingService();
            if (!string.IsNullOrEmpty(baseFolderPath))
            {
                _baseFolderPath = baseFolderPath;
            }
            IsEditMode = document != null;
            if (IsEditMode && document != null)
            {
                Title = "Chỉnh Sửa Hồ Sơ";
                TitleText.Text = "Chỉnh Sửa Hồ Sơ";
                SaveButton.Content = "Lưu Thay Đổi";
                TabAI.Visibility = Visibility.Collapsed;
                TabImport.Visibility = Visibility.Collapsed;
                BtnAutoFillAI.Visibility = Visibility.Visible;
                IsAIMode = false;
                AIContent.Visibility = Visibility.Collapsed;
                ManualContent.Visibility = Visibility.Visible;
                ContractNumberBox.Text = document.ContractNumber;
                SignDatePicker.SelectedDate = document.SignDate;
                ProjectNameBox.Text = document.ProjectName;
                ConstructionNameBox.Text = document.ConstructionName;
                ItemNameBox.Text = document.ItemName;
                EmployerNameBox.Text = document.EmployerName;
                ContractorNameBox.Text = document.ContractorName;
                EmployerCodeBox.Text = document.EmployerCode;
                ContractorCodeBox.Text = document.ContractorCode;
                ContractValueBox.Text = document.ContractValue.ToString("N0");
                PlannedAdvanceValueBox.Text = document.PlannedAdvanceValue.ToString("N0");
                LocationBox.Text = document.Location ?? "";
                InvestorNameBox.Text = document.InvestorName ?? "";
                IsCompletedCheckBox.IsChecked = document.IsCompleted;
                Document = document;
            }
            else
            {
                SignDatePicker.SelectedDate = DateTime.Today;
                AIContent.Visibility = Visibility.Collapsed;
                ManualContent.Visibility = Visibility.Visible;
            }
            SelectedFilesListAI.ItemsSource = _selectedAIFiles;
            Loaded += delegate
            {
                ApplyTheme(isDarkTheme);
            };
            Closed += delegate
            {
                CleanupResources();
            };
        }

        private void CleanupResources()
        {
            _aiCancellationTokenSource?.Cancel();
            _aiCancellationTokenSource?.Dispose();
            _aiCancellationTokenSource = null;
        }

        private void ApplyTheme(bool isDark)
        {
            Utilities.AppThemeResources.Apply(Resources, isDark);
            ResetDropZoneStyle();
            SolidColorBrush foreground = new(Color.FromRgb(30, 41, 59));
            SolidColorBrush foreground2 = new(Color.FromRgb(71, 85, 105));
            if (isDark)
            {
                MainBorder.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                TabSeparator.Background = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                MainBorder.Effect = new DropShadowEffect
                {
                    BlurRadius = 20.0,
                    ShadowDepth = 0.0,
                    Opacity = 0.3
                };
            }
            else
            {
                MainBorder.Background = Brushes.White;
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                TabSeparator.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                MainBorder.Effect = null;
                TitleText.Foreground = foreground;
                CancelButton.Foreground = foreground2;
                SolidColorBrush background = new(Color.FromRgb(248, 250, 252));
                SolidColorBrush borderBrush = new(Color.FromRgb(226, 232, 240));
                foreach (DependencyObject allChild in GetAllChildren(this))
                {
                    if (allChild is TextBlock textBlock)
                    {
                        if (textBlock.FontSize == 12.0)
                        {
                            textBlock.Foreground = foreground2;
                        }
                        else if (textBlock.Foreground is SolidColorBrush solidColorBrush && (solidColorBrush.Color == Color.FromRgb(241, 245, 249) || solidColorBrush.Color == Color.FromRgb(148, 163, 184)))
                        {
                            textBlock.Foreground = foreground;
                        }
                    }
                    else if (allChild is System.Windows.Controls.TextBox textBox)
                    {
                        textBox.Background = background;
                        textBox.BorderBrush = borderBrush;
                        textBox.Foreground = foreground;
                    }
                    else if (allChild is DatePicker datePicker)
                    {
                        datePicker.Background = background;
                        datePicker.BorderBrush = borderBrush;
                        datePicker.Foreground = foreground;
                    }
                }
            }
            var inputBackground = isDark
                ? new SolidColorBrush(Color.FromRgb(15, 23, 42))
                : new SolidColorBrush(Color.FromRgb(248, 250, 252));
            var inputBorder = isDark
                ? new SolidColorBrush(Color.FromRgb(51, 65, 85))
                : new SolidColorBrush(Color.FromRgb(226, 232, 240));
            var inputForeground = isDark
                ? new SolidColorBrush(Color.FromRgb(241, 245, 249))
                : new SolidColorBrush(Color.FromRgb(30, 41, 59));
            StyleDatePicker(SignDatePicker, inputBackground, inputBorder, inputForeground);

            if (IsEditMode)
            {
                UpdateTabStyles("Manual");
            }
            else if (IsImportMode)
            {
                UpdateTabStyles("Import");
            }
            else if (IsAIMode)
            {
                UpdateTabStyles("AI");
            }
            else
            {
                UpdateTabStyles("Manual");
            }
        }

        private static void StyleDatePicker(DatePicker datePicker, Brush background, Brush borderBrush, Brush foreground)
        {
            datePicker.Background = background;
            datePicker.BorderBrush = borderBrush;
            datePicker.Foreground = foreground;
            datePicker.ApplyTemplate();

            foreach (var child in GetAllChildren(datePicker))
            {
                switch (child)
                {
                    case DatePickerTextBox textBox:
                        textBox.Background = background;
                        textBox.BorderBrush = Brushes.Transparent;
                        textBox.Foreground = foreground;
                        textBox.CaretBrush = foreground;
                        break;
                    case Button button:
                        button.Foreground = foreground;
                        break;
                    case System.Windows.Shapes.Shape shape:
                        shape.Fill = foreground;
                        shape.Stroke = foreground;
                        break;
                }
            }
        }

        private static IEnumerable<DependencyObject> GetAllChildren(DependencyObject parent)
        {
            if (parent == null)
            {
                yield break;
            }
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;
                foreach (DependencyObject allChild in GetAllChildren(child))
                {
                    yield return allChild;
                }
            }
        }

        private void UpdateTabStyles(string activeTab)
        {
            SolidColorBrush foreground = (_isDarkTheme ? new SolidColorBrush(Color.FromRgb(241, 245, 249)) : new SolidColorBrush(Color.FromRgb(30, 41, 59)));
            SolidColorBrush foreground2 = new(Color.FromRgb(71, 85, 105));
            SolidColorBrush borderBrush = new(Color.FromRgb(59, 130, 246));
            TabAI.BorderBrush = Brushes.Transparent;
            TabAI.Foreground = foreground2;
            TabAI.FontWeight = FontWeights.Normal;
            TabManual.BorderBrush = Brushes.Transparent;
            TabManual.Foreground = foreground2;
            TabManual.FontWeight = FontWeights.Normal;
            TabImport.BorderBrush = Brushes.Transparent;
            TabImport.Foreground = foreground2;
            TabImport.FontWeight = FontWeights.Normal;
            
            Button button = activeTab switch
            {
                "AI" => TabAI,
                "Manual" => TabManual,
                "Import" => TabImport,
                _ => TabAI,
            };
            
            button.BorderBrush = borderBrush;
            button.Foreground = foreground;
            button.FontWeight = FontWeights.SemiBold;
        }

        private void TabAI_Click(object sender, RoutedEventArgs e)
        {
            IsAIMode = true;
            IsImportMode = false;
            AIContent.Visibility = Visibility.Visible;
            ManualContent.Visibility = Visibility.Collapsed;
            ImportContent.Visibility = Visibility.Collapsed;
            SaveButton.Content = "Tạo Hồ Sơ";
            CancelButton.Visibility = Visibility.Visible;
            UpdateTabStyles("AI");
        }

        private void TabManual_Click(object sender, RoutedEventArgs e)
        {
            IsAIMode = false;
            IsImportMode = false;
            AIContent.Visibility = Visibility.Collapsed;
            ManualContent.Visibility = Visibility.Visible;
            ImportContent.Visibility = Visibility.Collapsed;
            SaveButton.Content = "Tạo Hồ Sơ";
            CancelButton.Visibility = Visibility.Visible;
            UpdateTabStyles("Manual");
        }

        private void TabImport_Click(object sender, RoutedEventArgs e)
        {
            IsAIMode = false;
            IsImportMode = true;
            AIContent.Visibility = Visibility.Collapsed;
            ManualContent.Visibility = Visibility.Collapsed;
            ImportContent.Visibility = Visibility.Visible;
            SaveButton.Content = "Đóng";
            CancelButton.Visibility = Visibility.Collapsed;
            UpdateTabStyles("Import");
        }

        private void DropZone_Click(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialogForAI();
        }

        private void DropZone_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                ((string[])e.Data.GetData(DataFormats.FileDrop))
                    .Any(path => IsAIInputFile(path)))
            {
                e.Effects = DragDropEffects.Copy;
                DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                DropZone.Background = new SolidColorBrush(Color.FromRgb(30, 58, 95));
                DropZoneIcon.Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250));
                DropZoneText.Text = "Thả file vào đây...";
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DropZone_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            ResetDropZoneStyle();
        }

        private void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
        {
            ResetDropZoneStyle();
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFilesToList(files);
            }
        }

        private void ResetDropZoneStyle()
        {
            if (_isDarkTheme)
            {
                DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                DropZone.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            }
            else
            {
                DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                DropZone.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            }
            DropZoneIcon.Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            DropZoneText.Text = "Kéo thả file vào đây hoặc bấm để chọn";
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = MyRegex().IsMatch(e.Text);
        }

        [GeneratedRegex("[^0-9]+")]
        private static partial Regex MyRegex();

        private void OpenFileDialogForAI()
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Word and Excel Files (*.docx;*.xlsx)|*.docx;*.xlsx",
                Title = "Chọn file hợp đồng",
                Multiselect = true
            };
            if (openFileDialog.ShowDialog() == true)
            {
                AddFilesToList(openFileDialog.FileNames);
            }
        }

        private void AddFilesToList(string[] files)
        {
            bool rejectedFile = false;
            foreach (string text in files)
            {
                string value = Path.GetExtension(text).ToLower();
                if (IsAIInputFile(text) && !UploadedFilePaths.Contains(text))
                {
                    _selectedAIFiles.Add(new AIFileItem
                    {
                        FileName = Path.GetFileName(text),
                        FilePath = text
                    });
                    UploadedFilePaths.Add(text);
                }
                else if (!IsAIInputFile(text))
                {
                    rejectedFile = true;
                }
            }
            BtnReadWithAI.IsEnabled = _selectedAIFiles.Count > 0;
            if (rejectedFile)
            {
                AIStatusText.Text = "Chỉ nhận file Word (.docx) hoặc Excel (.xlsx). AI chỉ đọc file Word.";
                AIStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                AIStatusText.Visibility = Visibility.Visible;
            }
            else
            {
                bool hasExcel = UploadedFilePaths.Any(path => Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase));
                AIStatusText.Text = hasExcel
                    ? "Đã thêm file Word và Excel. Khi đọc AI, file Excel sẽ được bỏ qua."
                    : string.Empty;
                AIStatusText.Visibility = hasExcel ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static bool IsAIInputFile(string path)
        {
            var extension = Path.GetExtension(path);
            return extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string filePath })
            {
                AIFileItem? aIFileItem = _selectedAIFiles.FirstOrDefault(f => f.FilePath == filePath);
                if (aIFileItem != null)
                {
                    _selectedAIFiles.Remove(aIFileItem);
                    UploadedFilePaths.Remove(filePath);
                }
                BtnReadWithAI.IsEnabled = _selectedAIFiles.Count > 0;
            }
        }

        private async void BtnReadWithAI_Click(object sender, RoutedEventArgs e)
        {
            if (UploadedFilePaths.Count == 0)
            {
                return;
            }
            if (string.IsNullOrEmpty(_aiService.GetApiKey()))
            {
                AIStatusText.Text = "⚠\ufe0f Chưa cấu hình Gemini API Key. Vào Cài đặt để thêm.";
                AIStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                AIStatusText.Visibility = Visibility.Visible;
                return;
            }
            _aiCancellationTokenSource = new CancellationTokenSource();
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingText.Text = "Đang đọc file Word và trích xuất thông tin...";
            BtnReadWithAI.IsEnabled = false;
            AIStatusText.Visibility = Visibility.Collapsed;
            // Let WPF paint the overlay before starting file parsing or a local model call.
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            try
            {
                var wordFiles = UploadedFilePaths
                    .Where(path => Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (wordFiles.Count == 0)
                {
                    throw new InvalidOperationException("Bạn chưa chọn file Word (.docx). File Excel chỉ được đính kèm và sẽ không được dùng để đọc AI.");
                }

                ExtractedContractInfo? result = await _aiService.ExtractContractInfoAsync(wordFiles, _aiCancellationTokenSource.Token);
                if (_aiCancellationTokenSource.Token.IsCancellationRequested)
                {
                    return;
                }
                if (result != null)
                {
                    ContractNumberBox.Text = result.ContractNumber ?? "";
                    ProjectNameBox.Text = result.ProjectName ?? "";
                    ConstructionNameBox.Text = result.ConstructionName ?? "";
                    ItemNameBox.Text = result.ItemName ?? "";
                    if (result.SignDate.HasValue)
                    {
                        SignDatePicker.SelectedDate = result.SignDate.Value;
                    }
                    if (result.ContractValue.HasValue)
                    {
                        ContractValueBox.Text = result.ContractValue.Value.ToString("N0");
                    }
                    if (result.PlannedAdvanceValue.HasValue)
                    {
                        PlannedAdvanceValueBox.Text = result.PlannedAdvanceValue.Value.ToString("N0");
                    }
                    EmployerNameBox.Text = result.EmployerName ?? "";
                    InvestorNameBox.Text = result.InvestorName ?? "";
                    ContractorNameBox.Text = result.ContractorName ?? "";
                    EmployerCodeBox.Text = result.EmployerCode ?? "";
                    ContractorCodeBox.Text = result.ContractorCode ?? "";
                    LocationBox.Text = result.Location ?? "";
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    BtnReadWithAI.IsEnabled = true;
                    await Task.Delay(300);
                    SwitchToManualTabWithAIWarning();
                }
                else
                {
                    AIStatusText.Text = "⚠\ufe0f Không đọc được nội dung chữ từ file Word. Hãy kiểm tra file có nội dung và không bị khóa.";
                    AIStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    AIStatusText.Visibility = Visibility.Visible;
                }
            }
            catch (OperationCanceledException)
            {
                AIStatusText.Text = "⚠\ufe0f Đã hủy đọc file.";
                AIStatusText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                AIStatusText.Visibility = Visibility.Visible;
            }
            catch (Exception ex2)
            {
                AIStatusText.Text = "❌ Lỗi: " + ex2.Message;
                AIStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                AIStatusText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                BtnReadWithAI.IsEnabled = true;
                _aiCancellationTokenSource?.Dispose();
                _aiCancellationTokenSource = null;
            }
        }

        private void CancelAI_Click(object sender, RoutedEventArgs e)
        {
            _aiCancellationTokenSource?.Cancel();
            LoadingOverlay.Visibility = Visibility.Collapsed;
            BtnReadWithAI.IsEnabled = true;
        }

        private void SwitchToManualTabWithAIWarning()
        {
            IsAIMode = false;
            IsImportMode = false;
            AIContent.Visibility = Visibility.Collapsed;
            ManualContent.Visibility = Visibility.Visible;
            ImportContent.Visibility = Visibility.Collapsed;
            SaveButton.Content = "Tạo Hồ Sơ";
            CancelButton.Visibility = Visibility.Visible;
            UpdateTabStyles("Manual");
            AIWarningBanner.Visibility = Visibility.Visible;
        }

        private void BtnUploadFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Chọn file Excel để nhập",
                InitialDirectory = _baseFolderPath
            };
            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }
            try
            {
                ImportedDocuments = [];
                using XLWorkbook xLWorkbook = new(openFileDialog.FileName);
                IXLWorksheet iXLWorksheet = xLWorkbook.Worksheet(1);
                int num = 1;
                for (int i = 1; i <= 10; i++)
                {
                    string text = iXLWorksheet.Cell(i, 1).GetString().Trim();
                    if (text == "STT" || iXLWorksheet.Cell(i, 2).GetString().Contains("hợp đồng", StringComparison.OrdinalIgnoreCase))
                    {
                        num = i;
                        break;
                    }
                }
                int num2 = num + 1;
                int num3 = iXLWorksheet.LastRowUsed()?.RowNumber() ?? num2;
                for (int j = num2; j <= num3; j++)
                {
                    string text2 = iXLWorksheet.Cell(j, 2).GetString().Trim();
                    if (!string.IsNullOrEmpty(text2) && !text2.Contains("TỔNG", StringComparison.OrdinalIgnoreCase))
                    {
                        string projectName = iXLWorksheet.Cell(j, 3).GetString().Trim();
                        string itemName = iXLWorksheet.Cell(j, 4).GetString().Trim();
                        DateTime signDate = DateTime.Today;
                        try
                        {
                            signDate = iXLWorksheet.Cell(j, 5).GetDateTime();
                        }
                        catch
                        {
                        }
                        decimal contractValue = default;
                        try
                        {
                            contractValue = (decimal)iXLWorksheet.Cell(j, 6).GetDouble();
                        }
                        catch
                        {
                        }
                        ImportedDocuments.Add(new Document
                        {
                            ContractNumber = text2,
                            ProjectName = projectName,
                            ItemName = itemName,
                            SignDate = signDate,
                            ContractValue = contractValue,
                            PaymentStatus = "Chưa thanh toán",
                            CreatedDate = DateTime.Now
                        });
                    }
                }
                if (ImportedDocuments.Count > 0)
                {
                    ImportResultText.Text = $"✓ Đã đọc {ImportedDocuments.Count} hợp đồng từ file";
                    ImportResultText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    ImportResultText.Visibility = Visibility.Visible;
                    SaveButton.Content = "Nhập dữ liệu";
                }
                else
                {
                    ImportResultText.Text = "Không tìm thấy dữ liệu trong file";
                    ImportResultText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    ImportResultText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                ImportResultText.Text = "Lỗi đọc file: " + ex.Message;
                ImportResultText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                ImportResultText.Visibility = Visibility.Visible;
            }
        }

        private void BtnDownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new()
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = "MauNhapHopDong.xlsx",
                Title = "Lưu file mẫu",
                InitialDirectory = _baseFolderPath
            };
            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }
            try
            {
                using XLWorkbook xLWorkbook = new();
                IXLWorksheet iXLWorksheet = xLWorkbook.Worksheets.Add("DanhSach");
                iXLWorksheet.Cell(1, 1).Value = "STT";
                iXLWorksheet.Cell(1, 2).Value = "Số hợp đồng";
                iXLWorksheet.Cell(1, 3).Value = "Tên dự án";
                iXLWorksheet.Cell(1, 4).Value = "Hạng mục";
                iXLWorksheet.Cell(1, 5).Value = "Ngày ký";
                iXLWorksheet.Cell(1, 6).Value = "Giá trị HĐ";
                iXLWorksheet.Cell(2, 1).Value = 1;
                iXLWorksheet.Cell(2, 2).Value = "01/2025/HĐ-ABC";
                iXLWorksheet.Cell(2, 3).Value = "Dự án mẫu";
                iXLWorksheet.Cell(2, 4).Value = "Hạng mục 1";
                iXLWorksheet.Cell(2, 5).Value = DateTime.Today.ToString("dd/MM/yyyy");
                iXLWorksheet.Cell(2, 6).Value = 1000000000;
                xLWorkbook.SaveAs(saveFileDialog.FileName);
                _errorHandlingService.ShowSuccess("Đã tạo file mẫu thành công!");
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleError(ex, "Tạo file mẫu");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsImportMode)
            {
                if (ImportedDocuments != null && ImportedDocuments.Count > 0)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    DialogResult = false;
                    Close();
                }
                return;
            }
            if (string.IsNullOrWhiteSpace(ContractNumberBox.Text))
            {
                _errorHandlingService.ShowWarning("Vui lòng nhập số hợp đồng.");
                ContractNumberBox.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(ProjectNameBox.Text))
            {
                _errorHandlingService.ShowWarning("Vui lòng nhập tên dự án.");
                ProjectNameBox.Focus();
                return;
            }
            
            decimal val = 0;
            if (ContractValueBox != null && !string.IsNullOrWhiteSpace(ContractValueBox.Text))
            {
                string s = ContractValueBox.Text.Replace(",", "").Replace(".", "");
                decimal.TryParse(s, out val);
            }
            
            if (IsEditMode && Document != null)
            {
                Document.ContractNumber = ContractNumberBox.Text.Trim();
                Document.SignDate = SignDatePicker.SelectedDate ?? DateTime.Today;
                Document.ProjectName = ProjectNameBox.Text.Trim();
                Document.ConstructionName = ConstructionNameBox.Text.Trim();
                Document.ItemName = ItemNameBox.Text.Trim();
                Document.EmployerName = EmployerNameBox.Text.Trim();
                Document.InvestorName = InvestorNameBox.Text.Trim();
                Document.ContractorName = ContractorNameBox.Text.Trim();
                Document.EmployerCode = EmployerCodeBox.Text.Trim();
                Document.ContractorCode = ContractorCodeBox.Text.Trim();
                Document.ContractValue = val;
                
                if (VietnameseFormat.TryParseMoney(PlannedAdvanceValueBox.Text, out decimal advVal))
                    Document.PlannedAdvanceValue = advVal;

                Document.Location = LocationBox.Text.Trim();
                Document.IsCompleted = IsCompletedCheckBox.IsChecked ?? false;
            }
            else
            {
                Document = new Document
                {
                    ContractNumber = ContractNumberBox.Text.Trim(),
                    SignDate = (SignDatePicker.SelectedDate ?? DateTime.Today),
                    ProjectName = ProjectNameBox.Text.Trim(),
                    ConstructionName = ConstructionNameBox.Text.Trim(),
                    ItemName = ItemNameBox.Text.Trim(),
                    EmployerName = EmployerNameBox.Text.Trim(),
                    InvestorName = InvestorNameBox.Text.Trim(),
                    ContractorName = ContractorNameBox.Text.Trim(),
                    EmployerCode = EmployerCodeBox.Text.Trim(),
                    ContractorCode = ContractorCodeBox.Text.Trim(),
                    ContractValue = val,
                    PlannedAdvanceValue = VietnameseFormat.ParseMoneyOrZero(PlannedAdvanceValueBox.Text),
                    Location = LocationBox.Text.Trim(),
                    IsCompleted = IsCompletedCheckBox.IsChecked ?? false,
                    PaymentStatus = "Chưa thanh toán",
                    PaidPercentage = 0m,
                    Notes = null
                };
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
                {
                    textBox.Text = "";
                }
                textBox.SelectAll();
            }
        }

        private void ValueBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "0";
            }
        }

        private async void BtnAutoFillAI_Click(object sender, RoutedEventArgs e)
        {
            if (!IsEditMode || Document == null) return;
            
            if (string.IsNullOrEmpty(_aiService.GetApiKey()))
            {
                MessageBox.Show("Chưa cấu hình Gemini API Key. Vui lòng vào Cài đặt để thêm.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string inputFolder = Path.Combine(Document.FolderPath ?? _baseFolderPath, "1.INPUT");
            
            Microsoft.Win32.OpenFileDialog openFileDialog = new()
            {
                Filter = "Word Files (*.docx)|*.docx",
                Title = "Chọn file Hợp đồng để trích xuất",
                Multiselect = true,
                InitialDirectory = Directory.Exists(inputFolder) ? inputFolder : _baseFolderPath
            };

            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            var wordFiles = openFileDialog.FileNames.ToList();
            if (wordFiles.Count == 0)
            {
                return;
            }

            BtnAutoFillAI.IsEnabled = false;
            BtnAutoFillAI.Content = "Đang đọc AI...";
            _aiCancellationTokenSource = new CancellationTokenSource();

            try
            {
                var result = await _aiService.ExtractContractInfoAsync(wordFiles, _aiCancellationTokenSource.Token);
                if (_aiCancellationTokenSource.Token.IsCancellationRequested) return;

                if (result != null)
                {
                    bool updated = false;

                    if (!string.IsNullOrWhiteSpace(result.EmployerName) && string.IsNullOrWhiteSpace(EmployerNameBox.Text))
                    {
                        EmployerNameBox.Text = result.EmployerName;
                        updated = true;
                    }

                    if (!string.IsNullOrWhiteSpace(result.InvestorName) && string.IsNullOrWhiteSpace(InvestorNameBox.Text))
                    {
                        InvestorNameBox.Text = result.InvestorName;
                        updated = true;
                    }
                    
                    if (!string.IsNullOrWhiteSpace(result.ContractorName) && string.IsNullOrWhiteSpace(ContractorNameBox.Text))
                    {
                        ContractorNameBox.Text = result.ContractorName;
                        updated = true;
                    }

                    if (!string.IsNullOrWhiteSpace(result.EmployerCode) && string.IsNullOrWhiteSpace(EmployerCodeBox.Text))
                    {
                        EmployerCodeBox.Text = result.EmployerCode;
                        updated = true;
                    }

                    if (!string.IsNullOrWhiteSpace(result.ContractorCode) && string.IsNullOrWhiteSpace(ContractorCodeBox.Text))
                    {
                        ContractorCodeBox.Text = result.ContractorCode;
                        updated = true;
                    }

                    if (!string.IsNullOrWhiteSpace(result.Location))
                    {
                        LocationBox.Text = result.Location;
                        updated = true;
                    }

                    if (!string.IsNullOrWhiteSpace(result.ConstructionName) && string.IsNullOrWhiteSpace(ConstructionNameBox.Text))
                    {
                        ConstructionNameBox.Text = result.ConstructionName;
                        updated = true;
                    }

                    if (!string.IsNullOrWhiteSpace(result.ProjectName) && string.IsNullOrWhiteSpace(ProjectNameBox.Text))
                    {
                        ProjectNameBox.Text = result.ProjectName;
                        updated = true;
                    }

                    if (result.ItemName != null && string.IsNullOrWhiteSpace(ItemNameBox.Text))
                    {
                        ItemNameBox.Text = result.ItemName;
                        updated = true;
                    }

                    if (result.PlannedAdvanceValue.HasValue && (string.IsNullOrWhiteSpace(PlannedAdvanceValueBox.Text) || PlannedAdvanceValueBox.Text == "0"))
                    {
                        PlannedAdvanceValueBox.Text = result.PlannedAdvanceValue.Value.ToString("N0");
                        updated = true;
                    }

                    if (updated)
                    {
                        MessageBox.Show("Đã trích xuất và điền các trường dữ liệu thành công từ file Word.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Không tìm thấy dữ liệu mới nào hoặc tất cả các trường đã có sẵn dữ liệu.", "Thông tin", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                {
                    MessageBox.Show("Lỗi khi đọc file bằng AI: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                BtnAutoFillAI.IsEnabled = true;
                BtnAutoFillAI.Content = "🤖 Đọc AI (1.INPUT)";
                _aiCancellationTokenSource?.Dispose();
                _aiCancellationTokenSource = null;
            }
        }
    }
}
