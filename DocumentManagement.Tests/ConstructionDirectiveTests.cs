using System.IO;
using System.Text.Json;
using DocumentManagement.Models;
using DocumentManagement.Services;

namespace DocumentManagement.Tests;

public sealed class ConstructionDirectiveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "directive-tests-" + Guid.NewGuid().ToString("N"));
    private ConstructionDirectiveService Service => new(Path.Combine(_root, "store"));
    public ConstructionDirectiveTests() => Directory.CreateDirectory(_root);
    private string Source(string name = "ABC.2026.015 - Tên chỉ thị.pdf", string text = "original scan") { var path = Path.Combine(_root, name); File.WriteAllText(path, text); return path; }

    [Theory]
    [InlineData("ABC.2026.015 - Điều chỉnh.pdf", "ABC.2026.015", "Điều chỉnh", 2026)]
    [InlineData("ABC.2027- Tên.pdf", "ABC.2027", "Tên", 2027)]
    [InlineData("250.2023_Khánh Hưng.pdf", "250.2023", "Khánh Hưng", 2023)]
    [InlineData("68.2025_Khánh Hưng_Di dời đường ống.pdf", "68.2025", "Khánh Hưng Di dời đường ống", 2025)]
    [InlineData("182.2026_Khánh Hưng_Thi công.pdf", "182.2026", "Khánh Hưng Thi công", 2026)]
    public void ParserExtractsNumberTitleAndYear(string file, string number, string title, int year)
    {
        var item = ConstructionDirectiveService.ParseFileName(file);
        Assert.Equal(number, item.Number); Assert.Equal(title, item.Title); Assert.True(ConstructionDirectiveService.TryGetDirectiveYear(item.Number, out var actual)); Assert.Equal(year, actual);
    }

    [Fact]
    public void ImportKeepsSourceAndStoresOriginalNamedFileInYearFolder()
    {
        var source = Source(); var item = Service.Import(source, ConstructionDirectiveService.ParseFileName(source));
        Assert.Equal(Path.Combine(_root, "store", "2026", Path.GetFileName(source)), Service.GetFilePath(item));
        Assert.True(File.Exists(source)); Assert.Equal("original scan", File.ReadAllText(Service.GetFilePath(item)));
        Assert.True(File.Exists(Path.Combine(_root, "store", ".directive-management", "directives.json")));
    }

    [Fact]
    public void MissingYearRequiresMetadataCorrectionAndDoesNotCopy()
    {
        var source = Source("không có năm - Tên.pdf");
        Assert.Throws<InvalidDataException>(() => Service.Import(source, new ConstructionDirective { Number = "ABC", Title = "Tên" }));
        Assert.True(File.Exists(source)); Assert.False(Directory.Exists(Path.Combine(_root, "store", "files")));
    }

    [Fact]
    public void EditingNumberMovesScanToCorrectYearAndPreservesMetadata()
    {
        var source = Source(); var item = Service.Import(source, ConstructionDirectiveService.ParseFileName(source)); var oldPath = Service.GetFilePath(item);
        item.Number = "ABC.2027.015"; item.Title = "Tên mới"; var updated = Service.Update(item);
        Assert.False(File.Exists(oldPath)); Assert.Equal(Path.Combine(_root, "store", "2027", Path.GetFileName(source)), Service.GetFilePath(updated)); Assert.Equal("Tên mới", Assert.Single(Service.Load()).Title);
    }

    [Fact]
    public void NonEmptyRootIsAcceptedAndExistingContentIsUntouched()
    {
        var source = Source(); var entry = Service.Import(source, ConstructionDirectiveService.ParseFileName(source)); var chosen = Path.Combine(_root, "chosen"); Directory.CreateDirectory(chosen); File.WriteAllText(Path.Combine(chosen, "keep.txt"), "keep");
        Service.ChangeStorageRoot(chosen);
        Assert.Equal("keep", File.ReadAllText(Path.Combine(chosen, "keep.txt"))); Assert.True(File.Exists(Path.Combine(chosen, "2026", Path.GetFileName(source)))); Assert.True(File.Exists(Service.GetFilePath(entry)));
    }

    [Fact]
    public void MigratesLegacyAppDataCatalogToYearFolders()
    {
        var oldRoot = Path.Combine(_root, "legacy"); var legacy = new ConstructionDirectiveService(oldRoot); var source = Source(); var entry = legacy.Import(source, ConstructionDirectiveService.ParseFileName(source));
        var legacyCatalog = Path.Combine(oldRoot, "directives.json"); Directory.CreateDirectory(oldRoot); File.Copy(Path.Combine(oldRoot, ".directive-management", "directives.json"), legacyCatalog);
        var oldItem = JsonSerializer.Deserialize<List<ConstructionDirective>>(File.ReadAllText(legacyCatalog))!.Single(); var legacyFile = Path.Combine(oldRoot, "files", oldItem.Id.ToString("N") + ".pdf"); Directory.CreateDirectory(Path.GetDirectoryName(legacyFile)!); File.Copy(legacy.GetFilePath(entry), legacyFile); oldItem.StoredFileName = Path.GetFileName(legacyFile); File.WriteAllText(legacyCatalog, JsonSerializer.Serialize(new[] { oldItem })); File.Delete(Path.Combine(oldRoot, ".directive-management", "directives.json"));
        var destination = Path.Combine(_root, "destination"); legacy.ChangeStorageRoot(destination);
        Assert.True(File.Exists(Path.Combine(destination, "2026", Path.GetFileName(source)))); Assert.Equal(oldItem.Id, Assert.Single(legacy.Load()).Id);
    }

    [Fact]
    public void FailedCatalogCommitCleansNewCopyAndKeepsSource()
    {
        Directory.CreateDirectory(Path.Combine(_root, "store", ".directive-management", "directives.json")); var source = Source();
        Assert.ThrowsAny<Exception>(() => Service.Import(source, ConstructionDirectiveService.ParseFileName(source)));
        Assert.True(File.Exists(source)); Assert.False(File.Exists(Path.Combine(_root, "store", "2026", Path.GetFileName(source))));
    }

    [Fact]
    public void ScanExistingFilesDiscoversOnlyImmediateValidYearFoldersAndIsIdempotent()
    {
        var root = Path.Combine(_root, "store");
        var valid = Path.Combine(root, "2026", "ABC.2026.015 - Điều chỉnh.pdf");
        var wrongYear = Path.Combine(root, "2025", "ABC.2026.016 - Sai năm.pdf");
        var nested = Path.Combine(root, "2026", "nested", "ABC.2026.017 - Không quét.pdf");
        var invalid = Path.Combine(root, "2026", "khong-dung-ten.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(valid)!); Directory.CreateDirectory(Path.GetDirectoryName(wrongYear)!); Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllText(valid, "valid"); File.WriteAllText(wrongYear, "wrong"); File.WriteAllText(nested, "nested"); File.WriteAllText(invalid, "invalid");

        var first = Service.ScanExistingFiles();
        Assert.Equal(1, first.Added); Assert.Equal(2, first.Skipped.Count);
        var item = Assert.Single(Service.Load());
        Assert.Equal("ABC.2026.015", item.Number); Assert.Equal("Điều chỉnh", item.Title);
        Assert.Equal(Path.Combine("2026", Path.GetFileName(valid)), item.StoredFileName);
        Assert.Equal("valid", File.ReadAllText(valid));

        var second = Service.ScanExistingFiles();
        Assert.Equal(0, second.Added); Assert.Equal(1, second.SkippedDuplicates); Assert.Single(Service.Load());
    }

    [Fact]
    public void ScanExistingFilesDiscoversExistingUnderscoreNamedScansAndIsIdempotent()
    {
        var root = Path.Combine(_root, "store");
        var scans = new[]
        {
            (2023, "250.2023_Khánh Hưng.pdf", "250.2023", "Khánh Hưng"),
            (2025, "68.2025_Khánh Hưng_Di dời đường ống.pdf", "68.2025", "Khánh Hưng Di dời đường ống"),
            (2026, "182.2026_Khánh Hưng_Thi công.pdf", "182.2026", "Khánh Hưng Thi công")
        };
        foreach (var scan in scans)
        {
            var directory = Path.Combine(root, scan.Item1.ToString());
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, scan.Item2), "scan");
        }

        var first = Service.ScanExistingFiles();
        Assert.Equal(3, first.Added);
        var entries = Service.Load();
        foreach (var scan in scans)
        {
            var item = Assert.Single(entries.Where(x => x.Number == scan.Item3));
            Assert.Equal(scan.Item4, item.Title);
            Assert.Equal(Path.Combine(scan.Item1.ToString(), scan.Item2), item.StoredFileName);
        }

        var second = Service.ScanExistingFiles();
        Assert.Equal(0, second.Added);
        Assert.Equal(3, second.SkippedDuplicates);
    }

    [Fact]
    public void ScanExistingFilesDoesNotOverwriteCorruptCatalog()
    {
        var root = Path.Combine(_root, "store");
        Directory.CreateDirectory(Path.Combine(root, ".directive-management"));
        var catalog = Path.Combine(root, ".directive-management", "directives.json");
        File.WriteAllText(catalog, "not json");
        Directory.CreateDirectory(Path.Combine(root, "2026"));
        File.WriteAllText(Path.Combine(root, "2026", "ABC.2026 - Scan.pdf"), "scan");

        Assert.ThrowsAny<Exception>(() => Service.ScanExistingFiles());
        Assert.Equal("not json", File.ReadAllText(catalog));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
