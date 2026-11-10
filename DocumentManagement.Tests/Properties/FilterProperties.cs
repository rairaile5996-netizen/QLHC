using DocumentManagement.Models;
using DocumentManagement.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DocumentManagement.Tests.Properties;

/// <summary>
/// Property-based tests for document filtering operations.
/// </summary>
public class FilterProperties
{
    /// <summary>
    /// **Feature: document-management, Property 4: Filter Returns Only Matching Documents**
    /// For any list of Documents and any non-empty search text, all documents in the filter result
    /// must have ContractNumber, ProjectName or ItemName containing the search text (case-insensitive).
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FilterReturnsOnlyMatchingDocuments(NonEmptyString contractNumber, NonEmptyString projectName, NonEmptyString searchText)
    {
        // Skip whitespace-only search texts as they are treated as empty
        var searchString = searchText.Get;
        if (string.IsNullOrWhiteSpace(searchString))
            return true;

        var service = new DocumentService(null!);
        var documents = new List<Document>
        {
            new Document { ContractNumber = contractNumber.Get, ProjectName = projectName.Get, ItemName = "Test" },
            new Document { ContractNumber = "Unrelated", ProjectName = "Unrelated Project", ItemName = "Unrelated" },
            new Document { ContractNumber = searchString + "/HĐ", ProjectName = "Test Project", ItemName = "Test" },
            new Document { ContractNumber = "123/HĐ", ProjectName = searchString + " Project", ItemName = "Test" }
        };

        var filtered = service.FilterDocuments(documents, searchString).ToList();
        var lowerSearch = searchString.ToLowerInvariant();

        return filtered.All(d =>
            d.ContractNumber.ToLowerInvariant().Contains(lowerSearch) ||
            d.ProjectName.ToLowerInvariant().Contains(lowerSearch) ||
            d.ItemName.ToLowerInvariant().Contains(lowerSearch));
    }

    /// <summary>
    /// **Feature: document-management, Property 5: Empty Filter Returns All Documents**
    /// For any list of Documents, when filtering with empty or null search text,
    /// the result must contain all documents from the original list.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool EmptyFilterReturnsAllDocuments(NonEmptyString contractNumber, NonEmptyString projectName)
    {
        var service = new DocumentService(null!);
        var documents = new List<Document>
        {
            new Document { ContractNumber = contractNumber.Get, ProjectName = projectName.Get, ItemName = "Test" },
            new Document { ContractNumber = "456/HĐ", ProjectName = "Another Project", ItemName = "Another" }
        };

        var filteredWithNull = service.FilterDocuments(documents, null).ToList();
        var filteredWithEmpty = service.FilterDocuments(documents, "").ToList();
        var filteredWithWhitespace = service.FilterDocuments(documents, "   ").ToList();

        return filteredWithNull.Count == documents.Count &&
               filteredWithEmpty.Count == documents.Count &&
               filteredWithWhitespace.Count == documents.Count;
    }
}
