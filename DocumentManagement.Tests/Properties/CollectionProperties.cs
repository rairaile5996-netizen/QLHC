using DocumentManagement.Data;
using DocumentManagement.Models;
using DocumentManagement.Services;
using DocumentManagement.ViewModels;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Tests.Properties;

/// <summary>
/// Property-based tests for collection operations and UI state.
/// </summary>
public class CollectionProperties
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// **Feature: document-management, Property 7: Open Button State Based on FilePath**
    /// For any Document, if FilePath is null or empty, CanOpenFile must return false;
    /// if FilePath has a value, CanOpenFile must return true.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public static bool OpenButtonStateBasedOnFilePath(NonEmptyString contractNumber, NonEmptyString projectName, string? filePath)
    {
        using var context = CreateInMemoryContext();
        var service = new DocumentService(context);
        var viewModel = new MainViewModel(service, null!, null!, null!);

        var document = new Document
        {
            ContractNumber = contractNumber.Get,
            ProjectName = projectName.Get,
            FilePath = filePath
        };

        var canOpen = MainViewModel.CanOpenFile(document);
        var hasFilePath = !string.IsNullOrWhiteSpace(filePath);

        return canOpen == hasFilePath;
    }

    /// <summary>
    /// **Feature: document-management, Property 8: Document Collection Count After Create**
    /// For any list of Documents with n elements, after creating a new valid document,
    /// the list must have n+1 elements.
    /// **Validates: Requirements 2.2, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public static bool DocumentCollectionCountAfterCreate(NonEmptyString contractNumber, NonEmptyString projectName)
    {
        using var context = CreateInMemoryContext();
        var service = new DocumentService(context);

        var initialCount = service.GetAllDocumentsAsync().Result.Count();

        var newDocument = new Document
        {
            ContractNumber = contractNumber.Get,
            ProjectName = projectName.Get
        };

        service.CreateDocumentAsync(newDocument).Wait();

        var finalCount = service.GetAllDocumentsAsync().Result.Count();

        return finalCount == initialCount + 1;
    }

    /// <summary>
    /// **Feature: document-management, Property 9: Document Collection Count After Delete**
    /// For any list of Documents with n elements (n > 0), after deleting a document,
    /// the list must have n-1 elements.
    /// **Validates: Requirements 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public static bool DocumentCollectionCountAfterDelete(NonEmptyString contractNumber, NonEmptyString projectName)
    {
        using var context = CreateInMemoryContext();
        var service = new DocumentService(context);

        // Create a document first
        var document = new Document
        {
            ContractNumber = contractNumber.Get,
            ProjectName = projectName.Get
        };
        var createdDocument = service.CreateDocumentAsync(document).Result;

        var countBeforeDelete = service.GetAllDocumentsAsync().Result.Count();

        service.DeleteDocumentAsync(createdDocument.Id).Wait();

        var countAfterDelete = service.GetAllDocumentsAsync().Result.Count();

        return countAfterDelete == countBeforeDelete - 1;
    }
}
