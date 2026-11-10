using DocumentManagement.Data;
using DocumentManagement.Models;
using DocumentManagement.Services;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Tests.Properties;

/// <summary>
/// Property-based tests for document persistence operations.
/// </summary>
public class DocumentPersistenceProperties
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
    /// **Feature: document-management, Property 1: Document Persistence Round-Trip**
    /// For any valid Document with non-empty ContractNumber and ProjectName, when creating via DocumentService
    /// and then retrieving from database, the returned document must have the same values as the original.
    /// **Validates: Requirements 4.3, 10.2, 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DocumentPersistenceRoundTrip(NonEmptyString contractNumber, NonEmptyString projectName, string? notes, string? filePath)
    {
        using var context = CreateInMemoryContext();
        var service = new DocumentService(context);

        var originalDocument = new Document
        {
            ContractNumber = contractNumber.Get,
            ProjectName = projectName.Get,
            ItemName = "Test Item",
            Notes = notes,
            FilePath = filePath
        };

        var createdDocument = service.CreateDocumentAsync(originalDocument).Result;
        var retrievedDocument = service.GetDocumentByIdAsync(createdDocument.Id).Result;

        return retrievedDocument != null &&
               retrievedDocument.ContractNumber == contractNumber.Get &&
               retrievedDocument.ProjectName == projectName.Get &&
               retrievedDocument.Notes == notes &&
               retrievedDocument.FilePath == filePath;
    }

    /// <summary>
    /// **Feature: document-management, Property 2: Document Update Persistence**
    /// For any existing Document and any valid update data, when updating the document
    /// and then retrieving it, the document must reflect the updated values.
    /// **Validates: Requirements 5.2, 10.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DocumentUpdatePersistence(NonEmptyString originalContractNumber, NonEmptyString originalProjectName, 
        NonEmptyString newContractNumber, NonEmptyString newProjectName)
    {
        using var context = CreateInMemoryContext();
        var service = new DocumentService(context);

        var originalDocument = new Document
        {
            ContractNumber = originalContractNumber.Get,
            ProjectName = originalProjectName.Get,
            ItemName = "Test Item"
        };

        var createdDocument = service.CreateDocumentAsync(originalDocument).Result;

        createdDocument.ContractNumber = newContractNumber.Get;
        createdDocument.ProjectName = newProjectName.Get;

        service.UpdateDocumentAsync(createdDocument).Wait();
        var retrievedDocument = service.GetDocumentByIdAsync(createdDocument.Id).Result;

        return retrievedDocument != null &&
               retrievedDocument.ContractNumber == newContractNumber.Get &&
               retrievedDocument.ProjectName == newProjectName.Get;
    }

    /// <summary>
    /// **Feature: document-management, Property 3: Document Delete Removes From Database**
    /// For any Document that exists in the database, after deleting the document,
    /// retrieving that document must return null.
    /// **Validates: Requirements 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DocumentDeleteRemovesFromDatabase(NonEmptyString contractNumber, NonEmptyString projectName)
    {
        using var context = CreateInMemoryContext();
        var service = new DocumentService(context);

        var document = new Document
        {
            ContractNumber = contractNumber.Get,
            ProjectName = projectName.Get,
            ItemName = "Test Item"
        };

        var createdDocument = service.CreateDocumentAsync(document).Result;
        var documentId = createdDocument.Id;

        service.DeleteDocumentAsync(documentId).Wait();
        var retrievedDocument = service.GetDocumentByIdAsync(documentId).Result;

        return retrievedDocument == null;
    }

    /// <summary>
    /// **Feature: document-management, Property 6: File Path Preservation**
    /// For any valid file path string, when saving to Document and persisting to database,
    /// the retrieved file path must be identical to the original file path.
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FilePathPreservation(NonEmptyString contractNumber, NonEmptyString projectName, string? filePath)
    {
        using var context = CreateInMemoryContext();
        var service = new DocumentService(context);

        var document = new Document
        {
            ContractNumber = contractNumber.Get,
            ProjectName = projectName.Get,
            ItemName = "Test Item",
            FilePath = filePath
        };

        var createdDocument = service.CreateDocumentAsync(document).Result;
        var retrievedDocument = service.GetDocumentByIdAsync(createdDocument.Id).Result;

        return retrievedDocument != null && retrievedDocument.FilePath == filePath;
    }

    /// <summary>
    /// **Feature: folder-sync safety, Property: Sync Safety Valve**
    /// Sync must be skipped when more than half of tracked folders are missing
    /// (drive offline / base folder moved), and must never be skipped when
    /// nothing is missing or only a single document is tracked.
    /// </summary>
    [Property(MaxTest = 200)]
    public bool SyncSafetyValveDecision(PositiveInt tracked, NonNegativeInt missing)
    {
        var trackedCount = tracked.Get;
        var missingCount = Math.Min(missing.Get, trackedCount);

        var result = ViewModels.MainViewModel.ShouldSkipSyncForSafety(trackedCount, missingCount);

        // Never skip khi không có thư mục nào mất hoặc chỉ theo dõi 1 hồ sơ
        if (missingCount == 0 || trackedCount <= 1)
            return result == false;

        // Skip khi và chỉ khi mất quá nửa
        return result == (missingCount * 2 > trackedCount);
    }

    [Fact]
    public void SyncSafetyValve_AllFoldersMissing_SkipsSync()
    {
        Assert.True(ViewModels.MainViewModel.ShouldSkipSyncForSafety(trackedCount: 10, missingCount: 10));
    }

    [Fact]
    public void SyncSafetyValve_SingleMissingFolder_DoesNotSkip()
    {
        Assert.False(ViewModels.MainViewModel.ShouldSkipSyncForSafety(trackedCount: 10, missingCount: 1));
    }

    /// <summary>
    /// **Feature: folder-rename, Property: FilePath Rebase After Move**
    /// After a folder move, a FilePath inside the old folder must be rebased into the
    /// new folder with the same relative path; paths outside the old folder are untouched.
    /// </summary>
    [Fact]
    public void RebaseDocumentFilePath_FileInsideOldFolder_IsRebased()
    {
        var document = new Document
        {
            ContractNumber = "HD-01",
            ProjectName = "P",
            ItemName = "I",
            FilePath = @"D:\HO SO\HD-01\1.INPUT\contract.docx"
        };

        ViewModels.MainViewModel.RebaseDocumentFilePath(document, @"D:\HO SO\HD-01", @"D:\HO SO\HD-02");

        Assert.Equal(@"D:\HO SO\HD-02\1.INPUT\contract.docx", document.FilePath);
    }

    [Fact]
    public void RebaseDocumentFilePath_FileOutsideOldFolder_IsUntouched()
    {
        var document = new Document
        {
            ContractNumber = "HD-01",
            ProjectName = "P",
            ItemName = "I",
            FilePath = @"C:\Elsewhere\contract.docx"
        };

        ViewModels.MainViewModel.RebaseDocumentFilePath(document, @"D:\HO SO\HD-01", @"D:\HO SO\HD-02");

        Assert.Equal(@"C:\Elsewhere\contract.docx", document.FilePath);
    }

    [Fact]
    public void RebaseDocumentFilePath_NullOrEmptyFilePath_IsIgnored()
    {
        var document = new Document
        {
            ContractNumber = "HD-01",
            ProjectName = "P",
            ItemName = "I",
            FilePath = null
        };

        ViewModels.MainViewModel.RebaseDocumentFilePath(document, @"D:\HO SO\HD-01", @"D:\HO SO\HD-02");

        Assert.Null(document.FilePath);
    }
}
