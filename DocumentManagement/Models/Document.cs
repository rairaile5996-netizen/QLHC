using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace DocumentManagement.Models;

public class Document : INotifyPropertyChanged
{
    private bool _isFolderMissing;

    public int Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ConstructionName { get; set; } = string.Empty;
    public string? InvestorName { get; set; } = string.Empty; // Chủ đầu tư
    public string? EmployerName { get; set; } = string.Empty; // Bên giao thầu
    public string? ContractorName { get; set; } = string.Empty; // Bên nhận thầu
    public string? EmployerCode { get; set; } = string.Empty; // Ký hiệu bên giao thầu
    public string? ContractorCode { get; set; } = string.Empty; // Ký hiệu bên nhận thầu
    public DateTime SignDate { get; set; }
    public decimal ContractValue { get; set; }
    public decimal PaidPercentage { get; set; }
    public string PaymentStatus { get; set; } = "Chưa thanh toán";
    public string? Notes { get; set; }
    public string? Location { get; set; } // Địa điểm xây dựng
    public string? FilePath { get; set; }
    public string? FolderPath { get; set; }  // Đường dẫn thư mục lưu hồ sơ
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public decimal SettlementValue { get; set; }  // Giá trị quyết toán
    public decimal RemainingPaymentValue { get; set; }  // Giá trị còn lại thanh toán
    public decimal WarrantyValue { get; set; }    // Giá trị bảo hành
    public DateTime? WarrantyExpiryDate { get; set; }  // Ngày hết hạn bảo hành
    public bool HasWarrantyBond { get; set; }  // Xuất chứng thư bảo lãnh bảo hành
    public decimal PlannedAdvanceValue { get; set; } // Giá trị tạm ứng dự kiến theo HĐ gốc
    public bool IsCompleted { get; set; } // Trạng thái đã hoàn thành hay chưa

    [NotMapped]
    public bool IsFolderMissing
    {
        get => _isFolderMissing;
        set
        {
            if (_isFolderMissing == value) return;
            _isFolderMissing = value;
            OnPropertyChanged();
        }
    }

    // Computed properties
    public decimal TotalValue => ContractValue + (Appendices?.Sum(a => a.Value) ?? 0);
    public decimal TotalAppendixValue => Appendices?.Sum(a => a.Value) ?? 0;
    public decimal TotalAdvance => Advances?.Sum(a => a.Value) ?? 0;
    public decimal TotalPayment => Payments?.Sum(p => p.Value) ?? 0;
    public decimal RemainingAmount => TotalValue - TotalAdvance - TotalPayment;

    // Collections
    public List<ContractAppendix> Appendices { get; set; } = [];
    public List<AdvancePayment> Advances { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public List<InitialDocumentChecklistItem> InitialDocumentChecklist { get; set; } = [];
    public List<GuaranteeLetter> GuaranteeLetters { get; set; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void RefreshTotalValues()
    {
        OnPropertyChanged(nameof(TotalValue));
        OnPropertyChanged(nameof(TotalAppendixValue));
        OnPropertyChanged(nameof(TotalAdvance));
        OnPropertyChanged(nameof(TotalPayment));
        OnPropertyChanged(nameof(RemainingAmount));
        OnPropertyChanged(nameof(InvestorName));
        OnPropertyChanged(nameof(EmployerName));
        OnPropertyChanged(nameof(ContractorName));
        OnPropertyChanged(nameof(EmployerCode));
        OnPropertyChanged(nameof(ContractorCode));
    }
}

// Phụ lục hợp đồng
public class ContractAppendix
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string AppendixNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime SignDate { get; set; }
    public decimal Value { get; set; }
    public decimal PlannedAdvanceValue { get; set; } // Giá trị tạm ứng dự kiến theo PL này
}

// Tạm ứng
public class AdvancePayment
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int? RelatedAppendixId { get; set; } // Liên kết với PL (null nếu là HĐ chính)

    // Computed - not stored in DB
    public decimal Percentage { get; set; }
}

// Thanh toán
public class Payment
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Value { get; set; }

    // Computed - not stored in DB
    public decimal Percentage { get; set; }
}

public class InitialDocumentChecklistItem : INotifyPropertyChanged
{
    private string _status = "Chưa có";
    private DateTime? _receivedDate;
    private string? _filePath;
    private string? _note;

    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDone));
        }
    }

    public DateTime? ReceivedDate
    {
        get => _receivedDate;
        set
        {
            if (_receivedDate == value) return;
            _receivedDate = value;
            OnPropertyChanged();
        }
    }

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value) return;
            _filePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasFile));
        }
    }

    public string? Note
    {
        get => _note;
        set
        {
            if (_note == value) return;
            _note = value;
            OnPropertyChanged();
        }
    }

    public bool IsDone => Status == "Hoàn tất";
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
