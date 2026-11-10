using DocumentFormat.OpenXml.Packaging;
using System.IO;

namespace DocumentManagement.Services;

/// <summary>
/// Adds a small, portable marker to exported Word files so they can be
/// recognised after the user edits, moves, and copies them back.
/// </summary>
public static class ExportedDocumentMetadata
{
    private const string Marker = "DocumentManagement-Export";

    public static void Stamp(string path, string category)
    {
        if (!File.Exists(path) || !path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            using var document = WordprocessingDocument.Open(path, true);
            var properties = document.PackageProperties;
            properties.Title = $"{Marker}: {category}";
            properties.Subject = "Hồ sơ được xuất từ Document Management";
            properties.Keywords = $"{Marker};{category}";
            properties.Creator = "Document Management";
            properties.LastModifiedBy = "Document Management";
        }
        catch
        {
            // Metadata must never block a successful document export.
        }
    }

    public static bool TryRead(string path, out string category)
    {
        category = string.Empty;
        if (!File.Exists(path) || !path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            using var document = WordprocessingDocument.Open(path, false);
            var text = document.PackageProperties.Keywords;
            if (string.IsNullOrWhiteSpace(text) || !text.Contains(Marker, StringComparison.OrdinalIgnoreCase))
                return false;

            var parts = text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            category = parts.FirstOrDefault(part =>
                part.Equals("CVXD", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("VTĐV", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("HSPL", StringComparison.OrdinalIgnoreCase)) ?? "CVXD";
            return true;
        }
        catch
        {
            return false;
        }
    }
}
