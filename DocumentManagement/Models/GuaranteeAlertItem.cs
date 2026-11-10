namespace DocumentManagement.Models;

public class GuaranteeAlertItem
{
    public int GuaranteeId { get; set; }
    public int DocumentId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string LetterNumber { get; set; } = string.Empty;
    public string IssuingBank { get; set; } = string.Empty;
    public string RelationLabel { get; set; } = string.Empty;
    public GuaranteeType Type { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int DaysRemaining { get; set; }
    public GuaranteeAlertSeverity Severity { get; set; }

    public string TypeDisplay => Type switch
    {
        GuaranteeType.AdvancePayment => "Bảo lãnh tạm ứng",
        GuaranteeType.Performance => "Bảo lãnh thực hiện",
        GuaranteeType.Warranty => "Bảo lãnh bảo hành",
        _ => "Bảo lãnh"
    };

    public string SeverityDisplay => Severity switch
    {
        GuaranteeAlertSeverity.Warning => "Sắp hết hạn",
        GuaranteeAlertSeverity.Urgent => "Cần xử lý gấp",
        GuaranteeAlertSeverity.Overdue => "Đã quá hạn",
        _ => "Bình thường"
    };

    public string RemainingText => DaysRemaining switch
    {
        > 0 => $"Còn {DaysRemaining} ngày",
        0 => "Hết hạn hôm nay",
        _ => $"Quá hạn {Math.Abs(DaysRemaining)} ngày"
    };

    public string ExpiryText => ExpiryDate.ToString("dd/MM/yyyy");
}
