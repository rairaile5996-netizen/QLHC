using DocumentManagement.Data;
using DocumentManagement.Models;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Services;

public class GuaranteeService(AppDbContext context, GuaranteeFileService fileService) : IGuaranteeService
{
    private readonly AppDbContext _context = context;
    private readonly GuaranteeFileService _fileService = fileService;

    public async Task<IReadOnlyList<GuaranteeLetter>> GetGuaranteesForDocumentAsync(
        int documentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.GuaranteeLetters
            .AsNoTracking()
            .Where(g => g.DocumentId == documentId)
            .OrderBy(g => g.CreatedDate)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<GuaranteeLetter?> GetGuaranteeByIdAsync(
        int guaranteeId,
        CancellationToken cancellationToken = default)
    {
        return await _context.GuaranteeLetters
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == guaranteeId, cancellationToken);
    }

    public async Task<IReadOnlyList<GuaranteeAlertItem>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await _context.GuaranteeLetters
            .AsNoTracking()
            .Where(g => g.LifecycleStatus == GuaranteeLifecycleStatus.Active)
            .Select(g => new
            {
                g.Id,
                g.DocumentId,
                g.Type,
                g.LetterNumber,
                g.IssuingBank,
                g.RelatedAppendixLabel,
                g.ExpiryDate,
                ContractNumber = g.Document!.ContractNumber,
                ProjectName = g.Document!.ProjectName
            })
            .ToListAsync(cancellationToken);

        return candidates
            .Select(candidate =>
            {
                var daysRemaining = (candidate.ExpiryDate.Date - DateTime.Today).Days;
                var severity = GuaranteeStatusCalculator.GetAlertSeverity(
                    candidate.ExpiryDate,
                    GuaranteeLifecycleStatus.Active);

                return new GuaranteeAlertItem
                {
                    GuaranteeId = candidate.Id,
                    DocumentId = candidate.DocumentId,
                    ContractNumber = candidate.ContractNumber,
                    ProjectName = candidate.ProjectName,
                    LetterNumber = candidate.LetterNumber,
                    IssuingBank = candidate.IssuingBank,
                    RelationLabel = string.IsNullOrWhiteSpace(candidate.RelatedAppendixLabel)
                        ? "Hợp đồng gốc"
                        : candidate.RelatedAppendixLabel,
                    Type = candidate.Type,
                    ExpiryDate = candidate.ExpiryDate,
                    DaysRemaining = daysRemaining,
                    Severity = severity
                };
            })
            .Where(item => item.Severity != GuaranteeAlertSeverity.None)
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.ExpiryDate)
            .ThenBy(item => item.ContractNumber)
            .ToList();
    }

    public async Task<GuaranteeLetter> CreateGuaranteeAsync(
        GuaranteeUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request, requirePdf: true);

        var document = await LoadDocumentContextAsync(request.DocumentId, cancellationToken);
        var relatedAppendixId = request.Type == GuaranteeType.AdvancePayment ? request.RelatedAppendixId : null;
        var relationLabel = request.Type == GuaranteeType.AdvancePayment
            ? ResolveRelationLabel(document, relatedAppendixId, request.RelatedAppendixLabel)
            : string.Empty;
        var duplicateRootExists = await _context.GuaranteeLetters
            .AnyAsync(g =>
                g.DocumentId == document.Id &&
                g.ParentGuaranteeId == null &&
                g.RelatedAppendixId == relatedAppendixId &&
                g.Type == request.Type &&
                g.LifecycleStatus != GuaranteeLifecycleStatus.Cancelled,
                cancellationToken);

        if (duplicateRootExists)
        {
            throw new InvalidOperationException(
                $"Đã có thư {GetGuaranteeTypeDisplay(request.Type)} cho {relationLabel}. Hãy dùng 'Thêm tu chỉnh' nếu cần tạo thư mới trong chuỗi.");
        }

        var copiedPdfPath = await _fileService.CopyPdfAsync(
            request.SourcePdfPath!,
            document.FolderPath ?? string.Empty,
            relatedAppendixId,
            relationLabel,
            request.Type,
            cancellationToken);

        var entity = new GuaranteeLetter
        {
            DocumentId = document.Id,
            RelatedAppendixId = relatedAppendixId,
            RelatedAppendixLabel = relationLabel,
            Type = request.Type,
            LetterNumber = request.LetterNumber.Trim(),
            IssuingBank = request.IssuingBank.Trim(),
            GuaranteeValue = request.GuaranteeValue,
            IssueDate = request.IssueDate.Date,
            EffectiveDate = request.EffectiveDate.Date,
            ExpiryDate = request.ExpiryDate.Date,
            LifecycleStatus = GuaranteeLifecycleStatus.Active,
            Notes = NormalizeOptionalText(request.Notes),
            PdfFilePath = copiedPdfPath,
            CreatedDate = DateTime.Now
        };

        _context.GuaranteeLetters.Add(entity);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return entity;
        }
        catch
        {
            DeleteFileIfExists(copiedPdfPath);
            throw;
        }
    }

    public async Task<GuaranteeLetter> UpdateGuaranteeAsync(
        int guaranteeId,
        GuaranteeUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var guarantee = await _context.GuaranteeLetters
            .FirstOrDefaultAsync(g => g.Id == guaranteeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy bảo lãnh với mã {guaranteeId}.");

        ValidateRequest(request, requirePdf: false);

        var document = await LoadDocumentContextAsync(guarantee.DocumentId, cancellationToken);
        var relatedAppendixId = request.Type == GuaranteeType.AdvancePayment ? request.RelatedAppendixId : null;
        var relationLabel = request.Type == GuaranteeType.AdvancePayment
            ? ResolveRelationLabel(document, relatedAppendixId, request.RelatedAppendixLabel)
            : string.Empty;
        var originalPdfPath = guarantee.PdfFilePath;
        var preparedPdfPath = originalPdfPath;
        var rollbackMovedFile = false;

        try
        {
            if (!string.IsNullOrWhiteSpace(request.SourcePdfPath))
            {
                preparedPdfPath = await _fileService.CopyPdfAsync(
                    request.SourcePdfPath!,
                    document.FolderPath ?? string.Empty,
                    relatedAppendixId,
                    relationLabel,
                    request.Type,
                    cancellationToken);
            }
            else if (!string.Equals(guarantee.RelatedAppendixLabel, relationLabel, StringComparison.Ordinal))
            {
                preparedPdfPath = await _fileService.MoveCopiedPdfAsync(
                    guarantee.PdfFilePath,
                    document.FolderPath ?? string.Empty,
                    relatedAppendixId,
                    relationLabel,
                    request.Type,
                    cancellationToken);
                rollbackMovedFile = !string.Equals(preparedPdfPath, originalPdfPath, StringComparison.OrdinalIgnoreCase);
            }

            guarantee.RelatedAppendixId = relatedAppendixId;
            guarantee.RelatedAppendixLabel = relationLabel;
            guarantee.Type = request.Type;
            guarantee.LetterNumber = request.LetterNumber.Trim();
            guarantee.IssuingBank = request.IssuingBank.Trim();
            guarantee.GuaranteeValue = request.GuaranteeValue;
            guarantee.IssueDate = request.IssueDate.Date;
            guarantee.EffectiveDate = request.EffectiveDate.Date;
            guarantee.ExpiryDate = request.ExpiryDate.Date;
            guarantee.Notes = NormalizeOptionalText(request.Notes);
            guarantee.PdfFilePath = preparedPdfPath;
            guarantee.ModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            return guarantee;
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(request.SourcePdfPath) &&
                !string.Equals(preparedPdfPath, originalPdfPath, StringComparison.OrdinalIgnoreCase))
            {
                DeleteFileIfExists(preparedPdfPath);
            }
            else if (rollbackMovedFile &&
                     !string.Equals(preparedPdfPath, originalPdfPath, StringComparison.OrdinalIgnoreCase) &&
                     File.Exists(preparedPdfPath))
            {
                File.Move(preparedPdfPath, originalPdfPath);
            }

            throw;
        }
    }

    public async Task<GuaranteeLetter> CreateAmendmentAsync(
        int parentGuaranteeId,
        GuaranteeUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var parent = await _context.GuaranteeLetters
            .FirstOrDefaultAsync(g => g.Id == parentGuaranteeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy bảo lãnh gốc với mã {parentGuaranteeId}.");

        if (parent.LifecycleStatus != GuaranteeLifecycleStatus.Active)
        {
            throw new InvalidOperationException("Chỉ có thể tạo tu chỉnh từ bảo lãnh đang còn hiệu lực.");
        }

        if (request.Type != parent.Type || request.RelatedAppendixId != parent.RelatedAppendixId)
        {
            throw new InvalidOperationException(
                "Thư tu chỉnh phải giữ nguyên loại thư và liên kết HĐ/phụ lục của thư đang được tu chỉnh.");
        }

        ValidateRequest(request, requirePdf: true);

        var document = await LoadDocumentContextAsync(parent.DocumentId, cancellationToken);
        var copiedPdfPath = await _fileService.CopyPdfAsync(
            request.SourcePdfPath!,
            document.FolderPath ?? string.Empty,
            parent.RelatedAppendixId,
            parent.RelatedAppendixLabel ?? string.Empty,
            parent.Type,
            cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var amendment = new GuaranteeLetter
            {
                DocumentId = parent.DocumentId,
                RelatedAppendixId = parent.RelatedAppendixId,
                RelatedAppendixLabel = parent.RelatedAppendixLabel,
                ParentGuaranteeId = parent.Id,
                Type = request.Type,
                LetterNumber = request.LetterNumber.Trim(),
                IssuingBank = request.IssuingBank.Trim(),
                GuaranteeValue = request.GuaranteeValue,
                IssueDate = request.IssueDate.Date,
                EffectiveDate = request.EffectiveDate.Date,
                ExpiryDate = request.ExpiryDate.Date,
                LifecycleStatus = GuaranteeLifecycleStatus.Active,
                Notes = NormalizeOptionalText(request.Notes),
                PdfFilePath = copiedPdfPath,
                CreatedDate = DateTime.Now
            };

            _context.GuaranteeLetters.Add(amendment);
            parent.LifecycleStatus = GuaranteeLifecycleStatus.Replaced;
            parent.ModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return amendment;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            DeleteFileIfExists(copiedPdfPath);
            throw;
        }
    }

    public async Task DeleteGuaranteeAsync(
        int guaranteeId,
        CancellationToken cancellationToken = default)
    {
        var guarantee = await _context.GuaranteeLetters
            .FirstOrDefaultAsync(g => g.Id == guaranteeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy thư bảo lãnh với mã {guaranteeId}.");

        if (await _context.GuaranteeLetters
                .AnyAsync(g => g.ParentGuaranteeId == guaranteeId, cancellationToken))
        {
            throw new InvalidOperationException(
                "Không thể xóa thư này vì đã có thư tu chỉnh. Hãy giữ lại để bảo toàn lịch sử chuỗi bảo lãnh.");
        }

        var pdfPath = guarantee.PdfFilePath;
        _context.GuaranteeLetters.Remove(guarantee);
        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(pdfPath) && File.Exists(pdfPath))
        {
            try
            {
                File.Delete(pdfPath);
            }
            catch
            {
                // Database deletion is complete; a locked PDF should not leave a ghost record.
            }
        }
    }

    public async Task<GuaranteeLetter> UpdateLifecycleAsync(
        int guaranteeId,
        GuaranteeLifecycleStatus lifecycleStatus,
        CancellationToken cancellationToken = default)
    {
        if (lifecycleStatus is not (GuaranteeLifecycleStatus.Released or GuaranteeLifecycleStatus.Cancelled))
        {
            throw new InvalidOperationException("Chỉ được phép chuyển sang trạng thái Giải tỏa hoặc Hủy.");
        }

        var guarantee = await _context.GuaranteeLetters
            .FirstOrDefaultAsync(g => g.Id == guaranteeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy bảo lãnh với mã {guaranteeId}.");

        if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
        {
            throw new InvalidOperationException("Chỉ có thể cập nhật trạng thái cho bảo lãnh đang còn hiệu lực.");
        }

        guarantee.LifecycleStatus = lifecycleStatus;
        guarantee.ModifiedDate = DateTime.Now;
        await _context.SaveChangesAsync(cancellationToken);
        return guarantee;
    }

    public async Task OpenPdfAsync(int guaranteeId, CancellationToken cancellationToken = default)
    {
        var pdfPath = await _context.GuaranteeLetters
            .AsNoTracking()
            .Where(g => g.Id == guaranteeId)
            .Select(g => g.PdfFilePath)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            throw new KeyNotFoundException("Không tìm thấy PDF của bảo lãnh đã chọn.");
        }

        _fileService.OpenPdf(pdfPath);
    }

    private async Task<Document> LoadDocumentContextAsync(int documentId, CancellationToken cancellationToken)
    {
        var document = await _context.Documents
            .Include(d => d.Appendices)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ với mã {documentId}.");

        if (string.IsNullOrWhiteSpace(document.FolderPath))
        {
            throw new InvalidOperationException("Hồ sơ chưa có thư mục lưu trữ để sao chép PDF.");
        }

        return document;
    }

    private static string GetGuaranteeTypeDisplay(GuaranteeType type) => type switch
    {
        GuaranteeType.AdvancePayment => "bảo lãnh tạm ứng",
        GuaranteeType.Performance => "bảo lãnh thực hiện",
        GuaranteeType.Warranty => "bảo lãnh bảo hành",
        _ => "bảo lãnh"
    };

    private static void ValidateRequest(GuaranteeUpsertRequest request, bool requirePdf)
    {
        if (request.DocumentId <= 0)
        {
            throw new InvalidOperationException("Thiếu mã hồ sơ bảo lãnh.");
        }

        if (string.IsNullOrWhiteSpace(request.LetterNumber))
        {
            throw new InvalidOperationException("Số bảo lãnh là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(request.IssuingBank))
        {
            throw new InvalidOperationException("Ngân hàng phát hành là bắt buộc.");
        }

        if (request.GuaranteeValue <= 0)
        {
            throw new InvalidOperationException("Giá trị bảo lãnh phải lớn hơn 0.");
        }

        if (request.IssueDate.Date > request.ExpiryDate.Date)
        {
            throw new InvalidOperationException("Ngày phát hành không được sau ngày hết hạn.");
        }

        if (request.EffectiveDate.Date > request.ExpiryDate.Date)
        {
            throw new InvalidOperationException("Ngày hiệu lực không được sau ngày hết hạn.");
        }

        if (requirePdf)
        {
            GuaranteeFileService.EnsurePdfSource(request.SourcePdfPath);
        }
        else if (!string.IsNullOrWhiteSpace(request.SourcePdfPath))
        {
            GuaranteeFileService.EnsurePdfSource(request.SourcePdfPath);
        }
    }

    private static string ResolveRelationLabel(Document document, int? relatedAppendixId, string? requestedLabel)
    {
        if (relatedAppendixId == null)
        {
            return string.IsNullOrWhiteSpace(requestedLabel)
                ? "Hợp đồng gốc"
                : requestedLabel.Trim();
        }

        var appendix = document.Appendices.FirstOrDefault(a => a.Id == relatedAppendixId)
            ?? throw new InvalidOperationException("Phụ lục được chọn không thuộc hồ sơ hiện tại.");

        return $"Phụ lục {appendix.AppendixNumber}".Trim();
    }

    private static string? NormalizeOptionalText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static void DeleteFileIfExists(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        File.Delete(filePath);
    }
}
