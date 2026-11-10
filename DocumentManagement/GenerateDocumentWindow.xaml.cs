using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DocumentManagement.Models;
using DocumentManagement.Services;
using Microsoft.Win32;
using MaterialDesignThemes.Wpf;

namespace DocumentManagement
{
    public partial class GenerateDocumentWindow : Window
    {
        private readonly Document _contract;
        private readonly DocumentGeneratorService _generatorService;
        private readonly Dictionary<string, System.Windows.Controls.TextBox> _extraFields = [];
        private DocumentGeneratorService.DocumentType _selectedType;
        private string _selectedContractor = "Default";
        private string? _sourceWordFile;
        private string? _sourceExcelFile;
        private readonly ErrorHandlingService _errorHandlingService;

        public string? GeneratedFilePath { get; private set; }
        public string? DetectedContractor { get; private set; }

        public GenerateDocumentWindow(Document contract, AIService? aiService = null)
        {
            InitializeComponent();
            _contract = contract;
            _generatorService = new DocumentGeneratorService(aiService);
            _errorHandlingService = new ErrorHandlingService();

            DataContext = new { ContractInfo = $"HĐ: {contract.ContractNumber} - {contract.ProjectName}" };

            LoadDocumentTypes();
            LoadContractors();
            LoadPlaceholders();

            // Tự động detect contractor
            _ = AutoDetectContractorAsync();
        }

        private void LoadDocumentTypes()
        {
            var types = DocumentGeneratorService.GetAvailableDocumentTypes();
            foreach (var (type, name, desc, _) in types)
            {
                DocumentTypeCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = name, Tag = type, ToolTip = desc });
            }
            DocumentTypeCombo.SelectedIndex = 0;
        }

        private void LoadContractors()
        {
            var contractors = _generatorService.GetAvailableContractors();
            ContractorCombo.Items.Clear();

            // Thêm option tự động
            ContractorCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "🔍 Tự động nhận diện", Tag = "Auto" });

            foreach (var contractor in contractors)
            {
                ContractorCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = contractor, Tag = contractor });
            }
            ContractorCombo.SelectedIndex = 0;
        }

        private async Task AutoDetectContractorAsync()
        {
            // Tìm file Word trong thư mục INPUT
            if (string.IsNullOrEmpty(_contract.FolderPath)) return;

            var inputFolder = Path.Combine(_contract.FolderPath, "1.INPUT");
            if (!Directory.Exists(inputFolder)) return;

            var wordFiles = Directory.GetFiles(inputFolder, "*.docx", SearchOption.TopDirectoryOnly)
                .Where(f => !f.Contains("~$")).ToArray();
            if (wordFiles.Length == 0) return;

            try
            {
                var detected = await _generatorService.DetectContractorAsync(wordFiles[0]);
                if (!string.IsNullOrEmpty(detected) && detected != "Default")
                {
                    DetectedContractor = detected;
                    // Cập nhật UI
                    Dispatcher.Invoke(() =>
                    {
                        if (ContractorCombo.Items[0] is System.Windows.Controls.ComboBoxItem autoItem)
                            autoItem.Content = $"🔍 Tự động ({detected})";

                        UpdateTemplateStatus();
                    });
                }
            }
            catch { }
        }

        private void LoadPlaceholders()
        {
            var placeholders = DocumentGeneratorService.GetAvailablePlaceholders();
            PlaceholderList.ItemsSource = placeholders.Select(p => new { p.Placeholder, p.Description });
        }

        private void DocumentTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DocumentTypeCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is DocumentGeneratorService.DocumentType type)
            {
                _selectedType = type;
                UpdateTemplateStatus();
                LoadExtraFields(type);
            }
        }

        private void ContractorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContractorCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string contractor)
            {
                _selectedContractor = contractor == "Auto" ? (DetectedContractor ?? "Default") : contractor;
                UpdateTemplateStatus();
            }
        }

        private void UpdateTemplateStatus()
        {
            var contractor = _selectedContractor;
            if (ContractorCombo.SelectedItem is ComboBoxItem item && item.Tag as string == "Auto")
                contractor = DetectedContractor ?? "Default";

            var hasTemplate = _generatorService.HasTemplate(_selectedType, contractor);

            // Xác định đường dẫn template dựa trên loại công văn
            string templatePath;
            if (_selectedType == DocumentGeneratorService.DocumentType.PaymentRequest)
            {
                // Công văn thanh toán: dùng template chung ở root
                templatePath = Path.Combine(_generatorService.GetTemplateFolder(), "CongVanThanhToan.docx");
            }
            else
            {
                // Các loại khác: dùng template theo tổng thầu
                var templateFile = DocumentGeneratorService.GetAvailableDocumentTypes()
                    .FirstOrDefault(t => t.Type == _selectedType).TemplateFile;
                templatePath = Path.Combine(_generatorService.GetContractorTemplateFolder(contractor), templateFile ?? "");
            }

            if (hasTemplate)
            {
                TemplateIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
                TemplateIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));

                if (_selectedType == DocumentGeneratorService.DocumentType.PaymentRequest)
                {
                    TemplateStatusText.Text = "✓ Template chung (tất cả tổng thầu)";
                }
                else
                {
                    TemplateStatusText.Text = $"✓ Có template cho {contractor}";
                }
                TemplateStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            }
            else
            {
                TemplateIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
                TemplateIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                TemplateStatusText.Text = $"⚠ Chưa có template - sẽ tạo file mẫu";
                TemplateStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            }

            TemplatePathText.Text = templatePath;
        }

        private void LoadExtraFields(DocumentGeneratorService.DocumentType type)
        {
            ExtraFieldsPanel.Children.Clear();
            _extraFields.Clear();

            IEnumerable<(string Key, string Label, string DefaultValue)> fields = type switch
            {
                DocumentGeneratorService.DocumentType.PaymentRequest =>
                [
                    ("SoTienThanhToan", "Số tiền đề nghị thanh toán", _contract.RemainingAmount.ToString("N0"))
                ],
                DocumentGeneratorService.DocumentType.ContractLiquidation =>
                [
                    ("DaiDienBenA", "Đại diện Bên A", ""),
                    ("DaiDienBenB", "Đại diện Bên B", "")
                ],
                DocumentGeneratorService.DocumentType.HandoverReport =>
                [
                    ("DiaDiem", "Địa điểm công trình", ""),
                    ("ThoiGianBaoHanh", "Thời gian bảo hành (tháng)", "12")
                ],
                DocumentGeneratorService.DocumentType.LoadTest =>
                [
                    ("TaiThuNghiem", "Tải thử nghiệm (%)", "100"),
                    ("ThoiGianVanHanh", "Thời gian vận hành (giờ)", "72")
                ],
                _ => []
            };

            foreach (var (key, label, defaultValue) in fields)
                AddField(key, label, defaultValue);
        }

        private void AddField(string key, string label, string defaultValue)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var labelBlock = new TextBlock
            {
                Text = label, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            var textBox = new System.Windows.Controls.TextBox
            {
                Text = defaultValue,
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 12
            };
            _extraFields[key] = textBox;
            stack.Children.Add(labelBlock);
            stack.Children.Add(textBox);
            ExtraFieldsPanel.Children.Add(stack);
        }

        private void BrowseWordFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file Word hợp đồng",
                Filter = "Word files (*.docx)|*.docx",
                InitialDirectory = _contract.FolderPath ?? ""
            };
            if (dialog.ShowDialog() == true)
            {
                _sourceWordFile = dialog.FileName;
                SourceWordTextBox.Text = Path.GetFileName(dialog.FileName);
                SourceWordTextBox.Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
            }
        }

        private void BrowseExcelFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file Excel",
                Filter = "Excel files (*.xlsx)|*.xlsx",
                InitialDirectory = _contract.FolderPath ?? ""
            };
            if (dialog.ShowDialog() == true)
            {
                _sourceExcelFile = dialog.FileName;
                SourceExcelTextBox.Text = Path.GetFileName(dialog.FileName);
                SourceExcelTextBox.Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
            }
        }

        private void OpenTemplateFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = _generatorService.GetTemplateFolder();
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }

        private void OpenContractorFolder_Click(object sender, RoutedEventArgs e)
        {
            var contractor = _selectedContractor;
            if (ContractorCombo.SelectedItem is ComboBoxItem item && item.Tag as string == "Auto")
                contractor = DetectedContractor ?? "Default";

            var folder = _generatorService.GetContractorTemplateFolder(contractor);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GenerateButton.IsEnabled = false;
                GenerateButton.Content = "Đang tạo...";

                var extraData = new Dictionary<string, string>();
                foreach (var (key, textBox) in _extraFields)
                {
                    if (!string.IsNullOrWhiteSpace(textBox.Text))
                        extraData[key] = textBox.Text;
                }

                var contractor = _selectedContractor;
                if (ContractorCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag as string == "Auto")
                    contractor = null; // Để service tự detect

                var (filePath, detectedContractor) = await _generatorService.GenerateDocumentAsync(
                    _contract, _selectedType, extraData, _sourceWordFile, _sourceExcelFile, contractor);

                GeneratedFilePath = filePath;
                DetectedContractor = detectedContractor;

                var result = MessageBox.Show(
                    $"Đã tạo công văn thành công!\n\nTổng thầu: {detectedContractor}\nFile: {Path.GetFileName(filePath)}\n\nMở file?",
                    "Thành công", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleError(ex, "Tạo công văn");
            }
            finally
            {
                GenerateButton.IsEnabled = true;
                GenerateButton.Content = "Tạo công văn";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
