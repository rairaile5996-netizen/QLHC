using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DocumentManagement.Services;
using Microsoft.Win32;

namespace DocumentManagement
{
    /// <summary>
    /// Window quản lý thông tin tổng thầu
    /// Có thể xóa file này nếu không cần dùng
    /// </summary>
    public partial class ContractorManagerWindow : Window
    {
        private readonly DocumentGeneratorService _documentGeneratorService;
        private readonly AIService? _AIService;
        private readonly ErrorHandlingService _errorHandlingService;
        private readonly ObservableCollection<ContractorInfo> _contractors = new();
        private readonly ObservableCollection<ContractorInfo> _filteredContractors = new();
        private ContractorInfo? _currentContractor;
        private bool _isNewContractor;

        public ContractorManagerWindow(DocumentGeneratorService documentGeneratorService, AIService? AIService = null)
        {
            InitializeComponent();
            _documentGeneratorService = documentGeneratorService;
            _AIService = AIService;
            _errorHandlingService = new ErrorHandlingService();

            LoadContractors();
        }

        /// <summary>
        /// Load danh sách tổng thầu từ service
        /// </summary>
        private void LoadContractors()
        {
            _contractors.Clear();
            _filteredContractors.Clear();

            // Lấy trực tiếp từ contractors.json
            foreach (var info in _documentGeneratorService.GetAllContractors())
            {
                _contractors.Add(info);
                _filteredContractors.Add(info);
            }

            ContractorList.ItemsSource = _filteredContractors;
            UpdateContractorCount();

            if (_filteredContractors.Count > 0)
            {
                ContractorList.SelectedIndex = 0;
            }
            else
            {
                ClearForm();
            }
        }

        /// <summary>
        /// Cập nhật số lượng tổng thầu hiển thị
        /// </summary>
        private void UpdateContractorCount()
        {
            if (TotalContractorsText != null)
            {
                TotalContractorsText.Text = $"{_filteredContractors.Count} tổng thầu";
            }
        }

        /// <summary>
        /// Tìm kiếm tổng thầu
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text.Trim().ToLower();
            
            _filteredContractors.Clear();
            
            if (string.IsNullOrEmpty(searchText))
            {
                // Hiển thị tất cả
                foreach (var contractor in _contractors)
                {
                    _filteredContractors.Add(contractor);
                }
            }
            else
            {
                // Lọc theo tên hoặc mã
                foreach (var contractor in _contractors)
                {
                    if (contractor.Code.ToLower().Contains(searchText) || 
                        contractor.FullName.ToLower().Contains(searchText))
                    {
                        _filteredContractors.Add(contractor);
                    }
                }
            }
            
            UpdateContractorCount();
            
            // Chọn item đầu tiên nếu có
            if (_filteredContractors.Count > 0)
            {
                ContractorList.SelectedIndex = 0;
            }
            else
            {
                ClearForm();
            }
        }

        /// <summary>
        /// Khi chọn tổng thầu trong danh sách
        /// </summary>
        private void ContractorList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ContractorList.SelectedItem is ContractorInfo contractor)
            {
                _currentContractor = contractor;
                _isNewContractor = false;
                DisplayContractor(contractor);
            }
        }

        /// <summary>
        /// Hiển thị thông tin tổng thầu lên form
        /// </summary>
        private void DisplayContractor(ContractorInfo contractor)
        {
            CodeTextBox.Text = contractor.Code;
            FullNameTextBox.Text = contractor.FullName;
            DirectorTextBox.Text = contractor.Director;
            AddressTextBox.Text = contractor.Address;
            TaxCodeTextBox.Text = contractor.TaxCode;
            PhoneTextBox.Text = contractor.Phone;
            BankAccountTextBox.Text = contractor.BankAccount;
            BankNameTextBox.Text = contractor.BankName;

            // Disable mã khi đang sửa
            CodeTextBox.IsEnabled = _isNewContractor;
        }

        /// <summary>
        /// Xóa form
        /// </summary>
        private void ClearForm()
        {
            CodeTextBox.Text = string.Empty;
            FullNameTextBox.Text = string.Empty;
            DirectorTextBox.Text = string.Empty;
            AddressTextBox.Text = string.Empty;
            TaxCodeTextBox.Text = string.Empty;
            PhoneTextBox.Text = string.Empty;
            BankAccountTextBox.Text = string.Empty;
            BankNameTextBox.Text = string.Empty;
            CodeTextBox.IsEnabled = true;
            _currentContractor = null;
        }

        /// <summary>
        /// Thêm tổng thầu mới
        /// </summary>
        private void AddNewButton_Click(object sender, RoutedEventArgs e)
        {
            _isNewContractor = true;
            _currentContractor = new ContractorInfo();
            ClearForm();
            CodeTextBox.Focus();
        }

        /// <summary>
        /// Lưu thông tin tổng thầu
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            var code = CodeTextBox.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
            {
                _errorHandlingService.ShowWarning("Vui lòng nhập mã tổng thầu!");
                CodeTextBox.Focus();
                return;
            }

            var fullName = FullNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(fullName))
            {
                _errorHandlingService.ShowWarning("Vui lòng nhập tên công ty!");
                FullNameTextBox.Focus();
                return;
            }

            // Kiểm tra trùng mã khi thêm mới
            if (_isNewContractor && _contractors.Any(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            {
                _errorHandlingService.ShowWarning($"Mã tổng thầu '{code}' đã tồn tại!");
                CodeTextBox.Focus();
                return;
            }

            // Tạo hoặc cập nhật
            var contractor = new ContractorInfo
            {
                Code = code,
                FullName = fullName,
                Director = DirectorTextBox.Text.Trim(),
                Address = AddressTextBox.Text.Trim(),
                TaxCode = TaxCodeTextBox.Text.Trim(),
                Phone = PhoneTextBox.Text.Trim(),
                BankAccount = BankAccountTextBox.Text.Trim(),
                BankName = BankNameTextBox.Text.Trim(),
                LastUpdated = DateTime.Now
            };

            // Lưu vào service
            _documentGeneratorService.UpdateContractorInfo(contractor);

            // Cập nhật danh sách
            if (_isNewContractor)
            {
                _contractors.Add(contractor);
                _filteredContractors.Add(contractor);
                ContractorList.SelectedItem = contractor;
            }
            else
            {
                // Cập nhật item trong list
                var index = _contractors.IndexOf(_currentContractor!);
                if (index >= 0)
                {
                    _contractors[index] = contractor;
                }
                
                var filteredIndex = _filteredContractors.IndexOf(_currentContractor!);
                if (filteredIndex >= 0)
                {
                    _filteredContractors[filteredIndex] = contractor;
                }
                
                ContractorList.SelectedItem = contractor;
            }

            UpdateContractorCount();

            _isNewContractor = false;
            _currentContractor = contractor;
            CodeTextBox.IsEnabled = false;

            _errorHandlingService.ShowSuccess("Đã lưu thông tin tổng thầu!");
        }

        /// <summary>
        /// Xóa tổng thầu
        /// </summary>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentContractor == null || _isNewContractor)
            {
                _errorHandlingService.ShowWarning("Vui lòng chọn tổng thầu cần xóa!");
                return;
            }

            var result = MessageBox.Show(
                $"Bạn có chắc muốn xóa tổng thầu '{_currentContractor.Code}'?",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _contractors.Remove(_currentContractor);
                _filteredContractors.Remove(_currentContractor);
                _documentGeneratorService.DeleteContractorInfo(_currentContractor.Code);

                UpdateContractorCount();

                if (_filteredContractors.Count > 0)
                {
                    ContractorList.SelectedIndex = 0;
                }
                else
                {
                    ClearForm();
                }

                _errorHandlingService.ShowSuccess("Đã xóa tổng thầu!");
            }
        }

        /// <summary>
        /// AI đọc thông tin từ file Word hợp đồng
        /// </summary>
        private async void AIReadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_AIService == null)
            {
                _errorHandlingService.ShowWarning("Chưa cấu hình Gemini API Key!\nVui lòng vào Cài đặt để thêm API Key.");
                return;
            }

            var code = CodeTextBox.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
            {
                _errorHandlingService.ShowWarning("Vui lòng nhập mã tổng thầu trước!");
                CodeTextBox.Focus();
                return;
            }

            // Chọn file Word
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file Word hợp đồng",
                Filter = "Word Documents (*.docx)|*.docx|All files (*.*)|*.*",
                DefaultExt = ".docx"
            };

            if (dialog.ShowDialog() != true)
                return;

            // Disable button và hiển thị loading
            AIReadButton.IsEnabled = false;
            AIReadButton.Content = "⏳ Đang đọc...";

            try
            {
                var result = await _documentGeneratorService.ExtractContractorInfoFromWordAsync(dialog.FileName, code);

                if (result != null)
                {
                    // Cập nhật form
                    FullNameTextBox.Text = result.FullName;
                    DirectorTextBox.Text = result.Director;
                    AddressTextBox.Text = result.Address;
                    TaxCodeTextBox.Text = result.TaxCode;
                    PhoneTextBox.Text = result.Phone;
                    BankAccountTextBox.Text = result.BankAccount;
                    BankNameTextBox.Text = result.BankName;

                    _errorHandlingService.ShowSuccess("AI đã đọc xong thông tin từ hợp đồng!\nVui lòng kiểm tra và bấm Lưu.");
                }
                else
                {
                    _errorHandlingService.ShowWarning("Không thể đọc thông tin từ file.\nVui lòng kiểm tra file hoặc nhập thủ công.");
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleError(ex, "AI đọc hợp đồng");
            }
            finally
            {
                AIReadButton.IsEnabled = true;
                AIReadButton.Content = "🤖 AI đọc từ HĐ";
            }
        }

        /// <summary>
        /// Đóng cửa sổ
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
