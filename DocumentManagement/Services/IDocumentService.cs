using DocumentManagement.Models;

namespace DocumentManagement.Services;

public interface IDocumentService
{
    Task<IEnumerable<Document>> GetAllDocumentsAsync();
    Task<Document?> GetDocumentByIdAsync(int id);
    Task<Document> CreateDocumentAsync(Document document);
    Task<Document> UpdateDocumentAsync(Document document);
    Task DeleteDocumentAsync(int id);
    IEnumerable<Document> FilterDocuments(IEnumerable<Document> documents, string? searchText);
    Task AddAppendixAsync(int documentId, ContractAppendix appendix);
    Task UpdateAppendixAsync(ContractAppendix appendix);
    Task DeleteAppendixAsync(int appendixId);
    Task AddAdvanceAsync(int documentId, AdvancePayment advance);
    Task UpdateAdvanceAsync(AdvancePayment advance);
    Task DeleteAdvanceAsync(int advanceId);
    Task AddPaymentAsync(int documentId, Payment payment);
    Task UpdatePaymentAsync(Payment payment);
    Task DeletePaymentAsync(int paymentId);
    Task UpdateSettlementAsync(int documentId, decimal settlementValue);
    Task UpdateRemainingPaymentAsync(int documentId, decimal remainingPaymentValue);
    Task UpdateWarrantyAsync(int documentId, decimal warrantyValue);
    Task UpdateWarrantyExpiryAsync(int documentId, DateTime? warrantyExpiryDate);
    Task UpdateHasWarrantyBondAsync(int documentId, bool hasWarrantyBond);
    Task<List<InitialDocumentChecklistItem>> GetInitialDocumentChecklistAsync(int documentId);
    Task<InitialDocumentChecklistItem> SaveInitialDocumentChecklistItemAsync(InitialDocumentChecklistItem item);
    Task<List<InitialDocumentChecklistItem>> GetAllInitialDocumentChecklistAsync();
}
