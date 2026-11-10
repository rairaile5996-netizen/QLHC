using DocumentManagement.Data;
using DocumentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Services;

public class DocumentService(AppDbContext context) : IDocumentService
{
    private readonly AppDbContext _context = context;

    public async Task<IEnumerable<Document>> GetAllDocumentsAsync()
    {
        return await _context.Documents
            .Include(d => d.Appendices)
            .Include(d => d.Advances)
            .Include(d => d.Payments)
            .Include(d => d.InitialDocumentChecklist)
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync();
    }

    public async Task<Document?> GetDocumentByIdAsync(int id)
    {
        return await _context.Documents
            .Include(d => d.Appendices)
            .Include(d => d.Advances)
            .Include(d => d.Payments)
            .Include(d => d.InitialDocumentChecklist)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Document> CreateDocumentAsync(Document document)
    {
        document.CreatedDate = DateTime.Now;
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
        return document;
    }

    public async Task<Document> UpdateDocumentAsync(Document document)
    {
        var existingDocument = await _context.Documents
            .Include(d => d.Appendices)
            .Include(d => d.Advances)
            .Include(d => d.Payments)
            .Include(d => d.InitialDocumentChecklist)
            .FirstOrDefaultAsync(d => d.Id == document.Id)
            ?? throw new InvalidOperationException($"Document with ID {document.Id} not found.");

        existingDocument.ContractNumber = document.ContractNumber;
        existingDocument.ProjectName = document.ProjectName;
        existingDocument.ItemName = document.ItemName;
        existingDocument.ConstructionName = document.ConstructionName;
        existingDocument.SignDate = document.SignDate;
        existingDocument.ContractValue = document.ContractValue;
        existingDocument.PlannedAdvanceValue = document.PlannedAdvanceValue;
        existingDocument.EmployerName = document.EmployerName;
        existingDocument.ContractorName = document.ContractorName;
        existingDocument.EmployerCode = document.EmployerCode;
        existingDocument.ContractorCode = document.ContractorCode;
        existingDocument.PaidPercentage = document.PaidPercentage;
        existingDocument.PaymentStatus = document.PaymentStatus;
        existingDocument.IsCompleted = document.IsCompleted;
        existingDocument.Notes = document.Notes;
        existingDocument.Location = document.Location;
        existingDocument.InvestorName = document.InvestorName;
        existingDocument.FilePath = document.FilePath;
        existingDocument.FolderPath = document.FolderPath;
        existingDocument.ModifiedDate = DateTime.Now;

        // Chỉ update appendices nếu document có appendices mới
        // Không xóa appendices hiện có nếu document.Appendices rỗng
        if (document.Appendices is { Count: > 0 })
        {
            // Create a copy first because existingDocument.Appendices might be the same reference as document.Appendices
            var appendicesToUpdate = document.Appendices.ToList();

            existingDocument.Appendices.Clear();
            foreach (var appendix in appendicesToUpdate)
            {
                existingDocument.Appendices.Add(new ContractAppendix
                {
                    AppendixNumber = appendix.AppendixNumber,
                    Description = appendix.Description,
                    SignDate = appendix.SignDate,
                    Value = appendix.Value,
                    DocumentId = document.Id
                });
            }
        }

        await _context.SaveChangesAsync();
        return existingDocument;
    }

    public async Task DeleteDocumentAsync(int id)
    {
        var document = await _context.Documents.FindAsync(id)
            ?? throw new KeyNotFoundException($"Document with ID {id} not found.");
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();
    }

    public IEnumerable<Document> FilterDocuments(IEnumerable<Document> documents, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return documents;
        }

        var lowerSearchText = searchText.ToLowerInvariant();
        return documents.Where(d =>
            (d.ContractNumber?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (d.ProjectName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (d.ItemName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    public async Task AddAppendixAsync(int documentId, ContractAppendix appendix)
    {
        appendix.DocumentId = documentId;
        _context.ContractAppendices.Add(appendix);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAppendixAsync(ContractAppendix appendix)
    {
        var existing = await _context.ContractAppendices.FindAsync(appendix.Id)
            ?? throw new KeyNotFoundException($"Appendix with ID {appendix.Id} not found.");
        existing.AppendixNumber = appendix.AppendixNumber;
        existing.Description = appendix.Description;
        existing.SignDate = appendix.SignDate;
        existing.Value = appendix.Value;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAppendixAsync(int appendixId)
    {
        var appendix = await _context.ContractAppendices.FindAsync(appendixId)
            ?? throw new KeyNotFoundException($"Appendix with ID {appendixId} not found.");
        _context.ContractAppendices.Remove(appendix);
        await _context.SaveChangesAsync();
    }

    // Advance Payment methods
    public async Task AddAdvanceAsync(int documentId, AdvancePayment advance)
    {
        advance.DocumentId = documentId;
        _context.AdvancePayments.Add(advance);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAdvanceAsync(AdvancePayment advance)
    {
        var existing = await _context.AdvancePayments.FindAsync(advance.Id)
            ?? throw new KeyNotFoundException($"Advance payment with ID {advance.Id} not found.");
        existing.PaymentDate = advance.PaymentDate;
        existing.Description = advance.Description;
        existing.Value = advance.Value;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAdvanceAsync(int advanceId)
    {
        var advance = await _context.AdvancePayments.FindAsync(advanceId)
            ?? throw new KeyNotFoundException($"Advance payment with ID {advanceId} not found.");
        _context.AdvancePayments.Remove(advance);
        await _context.SaveChangesAsync();
    }

    // Payment methods
    public async Task AddPaymentAsync(int documentId, Payment payment)
    {
        payment.DocumentId = documentId;
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePaymentAsync(Payment payment)
    {
        var existing = await _context.Payments.FindAsync(payment.Id)
            ?? throw new KeyNotFoundException($"Payment with ID {payment.Id} not found.");
        existing.PaymentDate = payment.PaymentDate;
        existing.Description = payment.Description;
        existing.Value = payment.Value;
        await _context.SaveChangesAsync();
    }

    public async Task DeletePaymentAsync(int paymentId)
    {
        var payment = await _context.Payments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException($"Payment with ID {paymentId} not found.");
        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync();
    }

    // Settlement
    public async Task UpdateSettlementAsync(int documentId, decimal settlementValue)
    {
        var doc = await _context.Documents.FindAsync(documentId)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");
        doc.SettlementValue = settlementValue;
        await _context.SaveChangesAsync();
    }

    // Remaining Payment
    public async Task UpdateRemainingPaymentAsync(int documentId, decimal remainingPaymentValue)
    {
        var doc = await _context.Documents.FindAsync(documentId)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");
        doc.RemainingPaymentValue = remainingPaymentValue;
        await _context.SaveChangesAsync();
    }

    // Warranty
    public async Task UpdateWarrantyAsync(int documentId, decimal warrantyValue)
    {
        var doc = await _context.Documents.FindAsync(documentId)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");
        doc.WarrantyValue = warrantyValue;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateWarrantyExpiryAsync(int documentId, DateTime? warrantyExpiryDate)
    {
        var doc = await _context.Documents.FindAsync(documentId)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");
        doc.WarrantyExpiryDate = warrantyExpiryDate;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateHasWarrantyBondAsync(int documentId, bool hasWarrantyBond)
    {
        var doc = await _context.Documents.FindAsync(documentId)
            ?? throw new KeyNotFoundException($"Document with ID {documentId} not found.");
        doc.HasWarrantyBond = hasWarrantyBond;
        await _context.SaveChangesAsync();
    }

    public async Task<List<InitialDocumentChecklistItem>> GetInitialDocumentChecklistAsync(int documentId)
    {
        return await _context.InitialDocumentChecklistItems
            .Where(i => i.DocumentId == documentId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<List<InitialDocumentChecklistItem>> GetAllInitialDocumentChecklistAsync()
    {
        return await _context.InitialDocumentChecklistItems
            .OrderBy(i => i.DocumentId)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<InitialDocumentChecklistItem> SaveInitialDocumentChecklistItemAsync(InitialDocumentChecklistItem item)
    {
        if (item.Id == 0)
        {
            _context.InitialDocumentChecklistItems.Add(item);
        }
        else
        {
            var existing = await _context.InitialDocumentChecklistItems.FindAsync(item.Id)
                ?? throw new KeyNotFoundException($"Checklist item with ID {item.Id} not found.");

            existing.Name = item.Name;
            existing.Status = item.Status;
            existing.ReceivedDate = item.ReceivedDate;
            existing.FilePath = item.FilePath;
            existing.Note = item.Note;
            existing.SortOrder = item.SortOrder;
        }

        await _context.SaveChangesAsync();
        return item;
    }
}
