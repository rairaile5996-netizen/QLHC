using DocumentManagement.Models;

namespace DocumentManagement.Services;

public class GuaranteeUpsertRequest
{
    public int DocumentId { get; set; }
    public int? RelatedAppendixId { get; set; }
    public string? RelatedAppendixLabel { get; set; }
    public GuaranteeType Type { get; set; }
    public string LetterNumber { get; set; } = string.Empty;
    public string IssuingBank { get; set; } = string.Empty;
    public decimal GuaranteeValue { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string? Notes { get; set; }
    public string? SourcePdfPath { get; set; }
}

public interface IGuaranteeService
{
    Task<IReadOnlyList<GuaranteeLetter>> GetGuaranteesForDocumentAsync(int documentId, CancellationToken cancellationToken = default);
    Task<GuaranteeLetter?> GetGuaranteeByIdAsync(int guaranteeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GuaranteeAlertItem>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
    Task<GuaranteeLetter> CreateGuaranteeAsync(GuaranteeUpsertRequest request, CancellationToken cancellationToken = default);
    Task<GuaranteeLetter> UpdateGuaranteeAsync(int guaranteeId, GuaranteeUpsertRequest request, CancellationToken cancellationToken = default);
    Task<GuaranteeLetter> CreateAmendmentAsync(int parentGuaranteeId, GuaranteeUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteGuaranteeAsync(int guaranteeId, CancellationToken cancellationToken = default);
    Task<GuaranteeLetter> UpdateLifecycleAsync(int guaranteeId, GuaranteeLifecycleStatus lifecycleStatus, CancellationToken cancellationToken = default);
    Task OpenPdfAsync(int guaranteeId, CancellationToken cancellationToken = default);
}
