using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentManagement.Models;

public enum GuaranteeType
{
    AdvancePayment = 0,
    Performance = 1,
    Warranty = 2
}

public enum GuaranteeLifecycleStatus
{
    Active = 0,
    Replaced = 1,
    Released = 2,
    Cancelled = 3
}

public enum GuaranteeAlertSeverity
{
    None = 0,
    Warning = 1,
    Urgent = 2,
    Overdue = 3
}

public class GuaranteeLetter
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int? RelatedAppendixId { get; set; }
    public string? RelatedAppendixLabel { get; set; }
    public int? ParentGuaranteeId { get; set; }
    public GuaranteeType Type { get; set; }
    public string LetterNumber { get; set; } = string.Empty;
    public string IssuingBank { get; set; } = string.Empty;
    public decimal GuaranteeValue { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public GuaranteeLifecycleStatus LifecycleStatus { get; set; } = GuaranteeLifecycleStatus.Active;
    public string? Notes { get; set; }
    public string PdfFilePath { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public Document? Document { get; set; }
    public ContractAppendix? RelatedAppendix { get; set; }
    public GuaranteeLetter? ParentGuarantee { get; set; }
    public List<GuaranteeLetter> Amendments { get; set; } = [];

    [NotMapped]
    public int DaysRemaining => (ExpiryDate.Date - DateTime.Today).Days;

    [NotMapped]
    public GuaranteeAlertSeverity AlertSeverity =>
        GuaranteeStatusCalculator.GetAlertSeverity(ExpiryDate, LifecycleStatus);

    [NotMapped]
    public bool IsActionable => GuaranteeStatusCalculator.IsActionable(ExpiryDate, LifecycleStatus);

    [NotMapped]
    public string RelationDisplayLabel =>
        string.IsNullOrWhiteSpace(RelatedAppendixLabel) ? "Hợp đồng gốc" : RelatedAppendixLabel;

    [NotMapped]
    public string TypeDisplay => Type switch
    {
        GuaranteeType.AdvancePayment => "Bảo lãnh tạm ứng",
        GuaranteeType.Performance => "Bảo lãnh thực hiện",
        GuaranteeType.Warranty => "Bảo lãnh bảo hành",
        _ => "Bảo lãnh"
    };

    [NotMapped]
    public string LifecycleDisplay => LifecycleStatus switch
    {
        GuaranteeLifecycleStatus.Active => "Hiệu lực",
        GuaranteeLifecycleStatus.Replaced => "Đã thay thế",
        GuaranteeLifecycleStatus.Released => "Đã giải tỏa",
        GuaranteeLifecycleStatus.Cancelled => "Đã hủy",
        _ => "Không xác định"
    };

    [NotMapped]
    public string DisplayStatus => AlertSeverity switch
    {
        GuaranteeAlertSeverity.Warning => "Sắp hết hạn",
        GuaranteeAlertSeverity.Urgent => "Cần xử lý gấp",
        GuaranteeAlertSeverity.Overdue => "Đã quá hạn",
        _ => LifecycleDisplay
    };
}

public static class GuaranteeStatusCalculator
{
    public static GuaranteeAlertSeverity GetAlertSeverity(
        DateTime expiryDate,
        GuaranteeLifecycleStatus lifecycleStatus,
        DateTime? today = null)
    {
        if (lifecycleStatus != GuaranteeLifecycleStatus.Active)
        {
            return GuaranteeAlertSeverity.None;
        }

        var baseline = (today ?? DateTime.Today).Date;
        var daysRemaining = (expiryDate.Date - baseline).Days;

        if (daysRemaining < 0)
        {
            return GuaranteeAlertSeverity.Overdue;
        }

        if (daysRemaining <= 7)
        {
            return GuaranteeAlertSeverity.Urgent;
        }

        if (daysRemaining <= 30)
        {
            return GuaranteeAlertSeverity.Warning;
        }

        return GuaranteeAlertSeverity.None;
    }

    public static bool IsActionable(
        DateTime expiryDate,
        GuaranteeLifecycleStatus lifecycleStatus,
        DateTime? today = null)
    {
        return GetAlertSeverity(expiryDate, lifecycleStatus, today) != GuaranteeAlertSeverity.None;
    }
}
