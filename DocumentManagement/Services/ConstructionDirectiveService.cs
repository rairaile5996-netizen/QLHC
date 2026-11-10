using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentManagement.Models;

namespace DocumentManagement.Services;

/// <summary>Stores scans in a user-visible root: {root}\{year}\{original file name}.</summary>
public sealed class ConstructionDirectiveService
{
    private const string MetadataFolder = ".directive-management";
    private const string CatalogFile = "directives.json";
    private readonly object _gate = new();
    private readonly string? _preference;
    private string _root;
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp" };
    private static readonly Regex YearPattern = new(@"(?<!\d)(20\d{2})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex YearFolderPattern = new(@"^20\d{2}$", RegexOptions.Compiled);

    public ConstructionDirectiveService() : this(DefaultRoot, PreferencePath) { }
    private static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement", "ConstructionDirectives");
    private static string PreferencePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement", "directive-storage.json");
    public ConstructionDirectiveService(string root) => _root = Path.GetFullPath(root);
    public ConstructionDirectiveService(string root, string preference) : this(root)
    {
        _preference = preference;
        if (File.Exists(preference)) _root = Path.GetFullPath(JsonSerializer.Deserialize<string>(File.ReadAllText(preference)) ?? throw new InvalidDataException("Cấu hình thư mục chỉ thị không hợp lệ."));
    }

    public string StorageRoot => _root;
    public string MetadataPath => Path.Combine(_root, MetadataFolder);
    private string Catalog => Path.Combine(MetadataPath, CatalogFile);
    private string LegacyCatalog => Path.Combine(_root, CatalogFile);
    public static bool IsSupported(string path) => Extensions.Contains(Path.GetExtension(path));
    public static bool TryGetDirectiveYear(string number, out int year)
    {
        var match = YearPattern.Match(number ?? "");
        return int.TryParse(match.Success ? match.Value : null, out year);
    }
    public static ConstructionDirective ParseFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).Trim();
        var separator = Regex.Match(name, @"\s*-\s+");
        if (separator.Success)
        {
            return new ConstructionDirective
            {
                Number = name[..separator.Index].Trim(),
                Title = name[(separator.Index + separator.Length)..].Trim(),
                OriginalFileName = Path.GetFileName(path)
            };
        }

        // Existing construction scans commonly use "250.2023_Tên chỉ thị".
        // Only accept an underscore when the prefix is a compact directive number
        // containing a 20xx year, so unrelated underscore-named files are not added.
        var underscore = Regex.Match(name, @"^(?<number>[^\s_]*?(?<!\d)20\d{2}(?!\d)[^\s_]*)_(?<title>\S.*)$");
        if (underscore.Success)
        {
            return new ConstructionDirective
            {
                Number = underscore.Groups["number"].Value.Trim(),
                Title = Regex.Replace(underscore.Groups["title"].Value.Replace('_', ' '), @"\s+", " ").Trim(),
                OriginalFileName = Path.GetFileName(path)
            };
        }

        return new ConstructionDirective { Number = name, Title = "", OriginalFileName = Path.GetFileName(path) };
    }

    public void ChangeStorageRoot(string destination)
    {
        lock (_gate)
        {
            var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
            if (string.Equals(target, Path.TrimEndingDirectorySeparator(_root), StringComparison.OrdinalIgnoreCase)) return;
            var entries = Load(); var targetService = new ConstructionDirectiveService(target); var created = new List<string>();
            try
            {
                Directory.CreateDirectory(target);
                var migrated = new List<ConstructionDirective>();
                foreach (var entry in entries)
                {
                    var copy = Copy(entry);
                    var sourcePath = GetFilePath(entry);
                    copy.StoredFileName = targetService.BuildStoredName(RequireYear(copy.Number), copy.OriginalFileName, sourcePath);
                    var targetPath = targetService.GetFilePath(copy);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    CopyIfNeeded(sourcePath, targetPath, created);
                    migrated.Add(copy);
                }
                targetService.Save(migrated); targetService.Load(); SavePreference(target); _root = target;
            }
            catch { foreach (var file in created.AsEnumerable().Reverse()) if (File.Exists(file)) File.Delete(file); throw; }
        }
    }

    public List<ConstructionDirective> Load()
    {
        lock (_gate)
        {
            var path = File.Exists(Catalog) ? Catalog : LegacyCatalog;
            if (!File.Exists(path)) return [];
            var entries = JsonSerializer.Deserialize<List<ConstructionDirective>>(File.ReadAllText(path)) ?? throw CatalogError();
            var ids = new HashSet<Guid>(); var numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in entries)
            {
                if (item == null || item.Id == Guid.Empty || !ids.Add(item.Id) || string.IsNullOrWhiteSpace(item.Number) || !numbers.Add(item.Number.Trim()) || string.IsNullOrWhiteSpace(item.Title) || item.Project == null || string.IsNullOrWhiteSpace(item.OriginalFileName) || string.IsNullOrWhiteSpace(item.StoredFileName) || !new[] { "Chưa xử lý", "Đang xử lý", "Đã xử lý" }.Contains(item.Status) || item.ReceivedDate == default) throw CatalogError();
                if (!File.Exists(GetFilePath(item))) throw new FileNotFoundException("Không tìm thấy file scan của chỉ thị " + item.Number + ".");
            }
            return entries;
        }
    }

    public ConstructionDirective Import(string source, ConstructionDirective metadata)
    {
        lock (_gate)
        {
            if (!IsSupported(source)) throw new InvalidDataException("Chỉ hỗ trợ PDF và ảnh scan PNG, JPG, TIFF, BMP.");
            if (!File.Exists(source)) throw new FileNotFoundException("Không tìm thấy file scan nguồn.", source);
            var entries = Load(); var item = Normalize(metadata); item.Id = Guid.NewGuid(); item.OriginalFileName = Path.GetFileName(source);
            item.StoredFileName = BuildStoredName(RequireYear(item.Number), item.OriginalFileName, source); Validate(entries, item);
            var target = GetFilePath(item); var copied = false;
            try { Directory.CreateDirectory(Path.GetDirectoryName(target)!); if (!File.Exists(target)) { File.Copy(source, target, false); copied = true; } else if (!FilesEqual(source, target)) throw new IOException("File trùng tên nhưng khác nội dung."); entries.Add(item); Save(entries); return item; }
            catch { if (copied && File.Exists(target)) File.Delete(target); throw; }
        }
    }

    /// <summary>
    /// Adds valid scans which were already placed under {root}\20xx.  It never moves,
    /// copies, or renames those user-managed files.
    /// </summary>
    public ConstructionDirectiveScanResult ScanExistingFiles()
    {
        lock (_gate)
        {
            // Load first.  A corrupt catalog must remain untouched instead of being
            // replaced by a partial scan result.
            var entries = Load();
            var result = new ConstructionDirectiveScanResult();
            if (!Directory.Exists(_root)) return result;

            var knownNumbers = new HashSet<string>(entries.Select(x => NormalizeNumber(x.Number)), StringComparer.OrdinalIgnoreCase);
            var knownPaths = new HashSet<string>(entries.Select(x => NormalizeRelativePath(x.StoredFileName)), StringComparer.OrdinalIgnoreCase);
            foreach (var yearDirectory in Directory.EnumerateDirectories(_root)
                         .Where(path => YearFolderPattern.IsMatch(Path.GetFileName(path))))
            {
                var year = int.Parse(Path.GetFileName(yearDirectory));
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(yearDirectory, "*", SearchOption.TopDirectoryOnly).ToArray(); }
                catch (Exception ex) { result.AddSkipped($"{Path.GetFileName(yearDirectory)}: {ex.Message}"); continue; }

                foreach (var file in files)
                {
                    if (!IsSupported(file)) continue;
                    var parsed = ParseFileName(file);
                    if (string.IsNullOrWhiteSpace(parsed.Number) || string.IsNullOrWhiteSpace(parsed.Title))
                    {
                        result.AddSkipped($"{Path.GetFileName(file)}: tên file cần có dạng SỐ - Tên chỉ thị.");
                        continue;
                    }
                    if (!TryGetDirectiveYear(parsed.Number, out var numberYear) || numberYear != year)
                    {
                        result.AddSkipped($"{Path.GetFileName(file)}: năm trong số chỉ thị không khớp thư mục {year}.");
                        continue;
                    }

                    var relativePath = NormalizeRelativePath(Path.GetRelativePath(_root, file));
                    var number = NormalizeNumber(parsed.Number);
                    if (knownNumbers.Contains(number) || knownPaths.Contains(relativePath))
                    {
                        result.SkippedDuplicates++;
                        continue;
                    }

                    entries.Add(new ConstructionDirective
                    {
                        Id = Guid.NewGuid(), Number = parsed.Number.Trim(), Title = parsed.Title.Trim(),
                        OriginalFileName = Path.GetFileName(file), StoredFileName = Path.GetRelativePath(_root, file),
                        ReceivedDate = File.GetLastWriteTime(file).Date, Project = "", Status = "Chưa xử lý"
                    });
                    knownNumbers.Add(number);
                    knownPaths.Add(relativePath);
                    result.Added++;
                }
            }
            if (result.Added > 0) Save(entries);
            return result;
        }
    }

    public ConstructionDirective Update(ConstructionDirective metadata)
    {
        lock (_gate)
        {
            var entries = Load(); var index = entries.FindIndex(x => x.Id == metadata.Id);
            if (index < 0) throw new InvalidDataException("Không còn tìm thấy chỉ thị này. Hãy tải lại danh sách.");
            var original = entries[index]; var item = Normalize(metadata); item.OriginalFileName = original.OriginalFileName;
            item.StoredFileName = BuildStoredName(RequireYear(item.Number), original.OriginalFileName, GetFilePath(original), original.StoredFileName); Validate(entries, item);
            var oldPath = GetFilePath(original); var newPath = GetFilePath(item); var copied = false;
            try
            {
                if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) { Directory.CreateDirectory(Path.GetDirectoryName(newPath)!); if (!File.Exists(newPath)) { File.Copy(oldPath, newPath, false); copied = true; } else if (!FilesEqual(oldPath, newPath)) throw new IOException("File đích trùng tên nhưng khác nội dung."); }
                entries[index] = item; Save(entries);
                if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath)) File.Delete(oldPath);
                return item;
            }
            catch { if (copied && File.Exists(newPath)) File.Delete(newPath); throw; }
        }
    }

    public string GetFilePath(ConstructionDirective item)
    {
        if (string.IsNullOrWhiteSpace(item.StoredFileName) || Path.IsPathRooted(item.StoredFileName) || item.StoredFileName.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(x => x == "..")) throw new InvalidDataException("Đường dẫn file scan không hợp lệ.");
        var modern = Path.Combine(_root, item.StoredFileName);
        if (File.Exists(modern) || item.StoredFileName.Contains(Path.DirectorySeparatorChar) || item.StoredFileName.Contains(Path.AltDirectorySeparatorChar)) return modern;
        return Path.Combine(_root, "files", item.StoredFileName);
    }
    public static ConstructionDirective Copy(ConstructionDirective item) => new() { Id = item.Id, Number = item.Number, Title = item.Title, Project = item.Project, ReceivedDate = item.ReceivedDate, Status = item.Status, OriginalFileName = item.OriginalFileName, StoredFileName = item.StoredFileName };
    private static ConstructionDirective Normalize(ConstructionDirective metadata) { var item = Copy(metadata); item.Number = item.Number.Trim(); item.Title = item.Title.Trim(); item.Project = item.Project.Trim(); item.ReceivedDate = item.ReceivedDate.Date; return item; }
    private static string NormalizeNumber(string number) => number.Trim();
    private static string NormalizeRelativePath(string path) => path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim();
    private static int RequireYear(string number) => TryGetDirectiveYear(number, out var year) ? year : throw new InvalidDataException("Không tìm thấy năm 20xx trong số chỉ thị. Hãy sửa số chỉ thị (ví dụ ABC.2026.015) trước khi lưu.");
    private string BuildStoredName(int year, string originalName, string source, string? currentName = null)
    {
        var safeName = Path.GetFileName(originalName); var directory = Path.Combine(_root, year.ToString()); var candidate = Path.Combine(directory, safeName);
        if (currentName != null && string.Equals(Path.Combine(_root, currentName), candidate, StringComparison.OrdinalIgnoreCase)) return Path.Combine(year.ToString(), safeName);
        for (var n = 2; File.Exists(candidate) && !FilesEqual(source, candidate); n++) candidate = Path.Combine(directory, Path.GetFileNameWithoutExtension(safeName) + " (" + n + ")" + Path.GetExtension(safeName));
        return Path.GetRelativePath(_root, candidate);
    }
    private static void CopyIfNeeded(string source, string target, List<string>? created) { if (File.Exists(target)) { if (!FilesEqual(source, target)) throw new IOException("File trùng tên nhưng nội dung khác: " + Path.GetFileName(target)); return; } File.Copy(source, target, false); created?.Add(target); }
    private static bool FilesEqual(string left, string right) => new FileInfo(left).Length == new FileInfo(right).Length && SHA256.HashData(File.ReadAllBytes(left)).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(right)));
    private static void Validate(List<ConstructionDirective> entries, ConstructionDirective item) { if (string.IsNullOrWhiteSpace(item.Number) || string.IsNullOrWhiteSpace(item.Title)) throw new InvalidDataException("Vui lòng nhập đầy đủ số chỉ thị và tên chỉ thị."); if (!new[] { "Chưa xử lý", "Đang xử lý", "Đã xử lý" }.Contains(item.Status)) throw new InvalidDataException("Trạng thái chỉ thị không hợp lệ."); if (entries.Any(x => x.Id != item.Id && string.Equals(x.Number.Trim(), item.Number, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException($"Số chỉ thị {item.Number} đã tồn tại."); }
    private void Save(List<ConstructionDirective> entries) { Directory.CreateDirectory(MetadataPath); try { File.SetAttributes(MetadataPath, File.GetAttributes(MetadataPath) | FileAttributes.Hidden); } catch { /* metadata stays visible when the file system does not support hidden folders */ } var temp = Path.Combine(MetadataPath, Guid.NewGuid().ToString("N") + ".tmp"); try { using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { JsonSerializer.Serialize(stream, entries, new JsonSerializerOptions { WriteIndented = true }); stream.Flush(true); } File.Move(temp, Catalog, true); } finally { if (File.Exists(temp)) File.Delete(temp); } }
    private void SavePreference(string target) { if (_preference == null) return; Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_preference))!); var temp = _preference + "." + Guid.NewGuid().ToString("N") + ".tmp"; try { File.WriteAllText(temp, JsonSerializer.Serialize(target)); File.Move(temp, _preference, true); } finally { if (File.Exists(temp)) File.Delete(temp); } }
    private static InvalidDataException CatalogError() => new("Danh sách chỉ thị không hợp lệ. Dữ liệu được giữ nguyên; hãy kiểm tra file danh sách.");
}

public sealed class ConstructionDirectiveScanResult
{
    private readonly List<string> _skipped = [];
    public int Added { get; internal set; }
    public int SkippedDuplicates { get; internal set; }
    public IReadOnlyList<string> Skipped => _skipped;
    internal void AddSkipped(string message) => _skipped.Add(message);
    public string ToMessage() => $"Quét thư mục: thêm {Added} chỉ thị · trùng {SkippedDuplicates} · không hợp lệ {_skipped.Count}.";
}
