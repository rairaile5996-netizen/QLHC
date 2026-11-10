using System.Globalization;
using System.IO;
using System.Windows;
using DocumentManagement.Models;
using DocumentManagement.Services;

namespace DocumentManagement
{
    /// <summary>
    /// Dialog cho phép user xem và chỉnh sửa thông tin trước khi tạo công văn thanh toán
    /// </summary>
    public partial class PaymentRequestPreviewDialog : Window
    {
        private readonly Document _contract;
        private readonly string _excelFilePath;
        private readonly AIService? _AIService;
        private readonly DocumentGeneratorService _generatorService;

        /// <summary>
        /// Số tiền đề nghị thanh toán
        /// </summary>
        public decimal PaymentValue { get; private set; }

        /// <summary>
        /// Mô tả/Nội dung thanh toán (mặc định)
        /// </summary>
        public string Description => $"Thanh toán đợt - {_contract.ItemName}";

        /// <summary>
        /// Ngày công văn (mặc định hôm nay)
        /// </summary>
        public static DateTime DocumentDate => DateTime.Today;

        public PaymentRequestPreviewDialog(
            Document contract,
            string excelFilePath,
            AIService? AIService)
        {
            InitializeComponent();

            _contract = contract;
            _excelFilePath = excelFilePath;
            _AIService = AIService;
            _generatorService = new DocumentGeneratorService(AIService);

            // Populate contract info immediately (no blocking)
            var contractorCode = DocumentGeneratorService.ExtractContractorCode(contract.ContractNumber);
            var contractorInfo = _generatorService.GetContractorInfo(contractorCode);
            ContractorNameText.Text = contractorInfo?.FullName ?? contractorCode ?? "Chưa xác định";
            ContractNumberText.Text = contract.ContractNumber;
            ItemNameText.Text = contract.ItemName;
            SignDateText.Text = contract.SignDate.ToString("dd/MM/yyyy");

            // Set file source info
            SourceFileText.Text = Path.GetFileName(excelFilePath);

            // Load data when window opens
            Loaded += OnWindowLoaded;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Sử dụng Task.Delay để đảm bảo UI được render trước
                await Task.Delay(100);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnWindowLoaded error: {ex}");
                ShowError($"Lỗi khi tải dữ liệu: {ex.Message}");
            }
        }

        private async Task LoadDataAsync()
        {
            ShowLoading(true);

            try
            {
                decimal extractedValue = 0;
                string? note = null;

                // Thử đọc từ Excel trước
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Đang đọc file Excel: {_excelFilePath}");
                    var (success, value, desc) = await Task.Run(() =>
                        DocumentGeneratorService.ReadPaymentValueFromExcel(_excelFilePath));

                    if (success && value > 0)
                    {
                        extractedValue = value;
                        note = "Trích xuất từ file Excel";
                        System.Diagnostics.Debug.WriteLine($"Đọc Excel thành công: {value:N0}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Không đọc được giá trị từ Excel");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi đọc Excel: {ex.Message}");
                }

                // Nếu không đọc được từ Excel, dùng giá trị từ hợp đồng
                if (extractedValue == 0)
                {
                    extractedValue = _contract.RemainingAmount;
                    note = "Sử dụng giá trị còn lại từ hợp đồng";

                    // Nếu remaining amount = 0, dùng contract value
                    if (extractedValue == 0)
                    {
                        extractedValue = _contract.ContractValue;
                        note = "Sử dụng giá trị hợp đồng";
                    }
                    System.Diagnostics.Debug.WriteLine($"Sử dụng giá trị từ hợp đồng: {extractedValue:N0}");
                }

                // Cập nhật UI
                PaymentValue = extractedValue;
                PaymentValueBox.Text = extractedValue.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
                PaymentValueNote.Text = note ?? "";

                ShowLoading(false);
                CreateButton.IsEnabled = true;

                System.Diagnostics.Debug.WriteLine("LoadDataAsync hoàn thành thành công");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadDataAsync error: {ex}");
                ShowError($"Không thể đọc thông tin từ file:\n{ex.Message}");
            }
        }

        private void ShowLoading(bool isLoading)
        {
            LoadingPanel.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            ContentPanel.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;
            CreateButton.IsEnabled = !isLoading;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            LoadingPanel.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            CreateButton.IsEnabled = false;
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // Parse payment value
            var valueText = PaymentValueBox.Text.Replace(".", "").Replace(",", "").Trim();
            if (decimal.TryParse(valueText, out var value))
            {
                PaymentValue = value;
            }
            else
            {
                MessageBox.Show("Số tiền không hợp lệ!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                PaymentValueBox.Focus();
                return;
            }

            if (PaymentValue <= 0)
            {
                MessageBox.Show("Số tiền phải lớn hơn 0!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                PaymentValueBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
