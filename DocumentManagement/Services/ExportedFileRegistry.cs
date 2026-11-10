using System.IO;
using System.Text.Json;

namespace DocumentManagement.Services;

public sealed record ExportedFileEntry(string Path, string Category, DateTime Created);

public static class ExportedFileRegistry
{
    private static readonly object Gate = new();
    private static string RegistryPath(string root) => System.IO.Path.Combine(root, ".app-exported-files.json");

    public static List<ExportedFileEntry> Read(string root)
    {
        lock (Gate)
        {
            var path = RegistryPath(root);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<List<ExportedFileEntry>>(File.ReadAllText(path)) ?? []
                : [];
        }
    }

    private static void Save(string root, List<ExportedFileEntry> entries)
    {
        var path = RegistryPath(root);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }

    public static void Record(string? root, string path, string category)
    {
        root = string.IsNullOrWhiteSpace(root) ? AppContext.BaseDirectory : root;
        ExportedDocumentMetadata.Stamp(path, category);
        lock (Gate)
        {
            Directory.CreateDirectory(root);
            var entries = Read(root);
            var relative = System.IO.Path.GetRelativePath(root, System.IO.Path.GetFullPath(path));
            entries.RemoveAll(e => string.Equals(e.Path, relative, StringComparison.OrdinalIgnoreCase));
            entries.Add(new(relative, category, DateTime.Now));
            Save(root, entries);
        }
    }

    public static void Rename(string root, ExportedFileEntry entry, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName != newName.Trim() || newName.EndsWith('.') || newName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            throw new IOException("Tên file không hợp lệ.");
        lock (Gate)
        {
            var entries = Read(root);
            var index = entries.FindIndex(e => e == entry);
            if (index < 0) throw new IOException("Danh sách đã thay đổi. Hãy làm mới.");
            var oldPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, entry.Path));
            var newPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(oldPath)!, newName + System.IO.Path.GetExtension(oldPath));
            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return;
            if (File.Exists(newPath)) throw new IOException("Đã có file trùng tên trong thư mục.");
            File.Move(oldPath, newPath);
            entries[index] = entry with { Path = System.IO.Path.GetRelativePath(root, newPath) };
            try { Save(root, entries); }
            catch { File.Move(newPath, oldPath); throw; }
        }
    }
}
