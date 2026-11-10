using System.Diagnostics;
using System.IO;
using System.Text;
using DocumentManagement.Models;

namespace DocumentManagement.Services;

public class GuaranteeFileService
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public async Task<string> CopyPdfAsync(
        string sourcePdfPath,
        string contractFolderPath,
        int? relatedAppendixId,
        string relationLabel,
        GuaranteeType type = GuaranteeType.AdvancePayment,
        CancellationToken cancellationToken = default)
    {
        EnsurePdfSource(sourcePdfPath);
        var destinationFolder = GetDestinationFolder(contractFolderPath, relatedAppendixId, relationLabel, type);
        Directory.CreateDirectory(destinationFolder);

        var destinationPath = GetAvailableFilePath(destinationFolder, Path.GetFileName(sourcePdfPath));
        await CopyFileAsync(sourcePdfPath, destinationPath, cancellationToken);
        return destinationPath;
    }

    public Task<string> MoveCopiedPdfAsync(
        string existingPdfPath,
        string contractFolderPath,
        int? relatedAppendixId,
        string relationLabel,
        GuaranteeType type = GuaranteeType.AdvancePayment,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(existingPdfPath))
        {
            throw new InvalidOperationException("Không tìm thấy đường dẫn PDF đã lưu.");
        }

        var destinationFolder = GetDestinationFolder(contractFolderPath, relatedAppendixId, relationLabel, type);
        Directory.CreateDirectory(destinationFolder);

        var destinationPath = GetAvailableFilePath(destinationFolder, Path.GetFileName(existingPdfPath));
        if (string.Equals(existingPdfPath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(existingPdfPath);
        }

        if (!File.Exists(existingPdfPath))
        {
            return Task.FromResult(existingPdfPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        File.Move(existingPdfPath, destinationPath);
        return Task.FromResult(destinationPath);
    }

    public void OpenPdf(string pdfFilePath)
    {
        if (string.IsNullOrWhiteSpace(pdfFilePath))
        {
            throw new InvalidOperationException("Không tìm thấy đường dẫn PDF đã lưu.");
        }

        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException("Không tìm thấy bản PDF đã sao chép.", pdfFilePath);
        }

        Process.Start(new ProcessStartInfo(pdfFilePath) { UseShellExecute = true });
    }

    public static bool IsPdfFileMissing(string? pdfFilePath)
    {
        return !string.IsNullOrWhiteSpace(pdfFilePath) && !File.Exists(pdfFilePath);
    }

    public static void EnsurePdfSource(string? sourcePdfPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePdfPath))
        {
            throw new InvalidOperationException("Vui lòng chọn tệp PDF.");
        }

        if (!File.Exists(sourcePdfPath))
        {
            throw new FileNotFoundException("Không tìm thấy tệp PDF nguồn.", sourcePdfPath);
        }

        if (!string.Equals(Path.GetExtension(sourcePdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chỉ chấp nhận tệp PDF.");
        }
    }

    public string GetDestinationFolder(string contractFolderPath, int? relatedAppendixId, string relationLabel, GuaranteeType type = GuaranteeType.AdvancePayment)
    {
        if (string.IsNullOrWhiteSpace(contractFolderPath))
        {
            throw new InvalidOperationException("Hồ sơ chưa có thư mục lưu trữ để sao chép PDF.");
        }

        var guaranteeRoot = Path.Combine(contractFolderPath, type switch
        {
            GuaranteeType.AdvancePayment => "4.HSTU",
            GuaranteeType.Warranty => "7.KTBH",
            GuaranteeType.Performance => "1.INPUT",
            _ => "4.HSTU"
        });
        if (type != GuaranteeType.AdvancePayment)
        {
            return guaranteeRoot;
        }

        if (relatedAppendixId == null)
        {
            return Path.Combine(guaranteeRoot, "HD GOC");
        }

        var appendixSegment = SanitizeFolderSegment(ExtractAppendixSegment(relationLabel));
        if (string.IsNullOrWhiteSpace(appendixSegment))
        {
            appendixSegment = "KHONG-XAC-DINH";
        }

        return Path.Combine(guaranteeRoot, $"PHU LUC {appendixSegment}");
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destinationStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
    }

    private static string ExtractAppendixSegment(string relationLabel)
    {
        if (string.IsNullOrWhiteSpace(relationLabel))
        {
            return string.Empty;
        }

        var trimmed = relationLabel.Trim();
        if (trimmed.StartsWith("Phụ lục ", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed["Phụ lục ".Length..].Trim();
        }

        return trimmed;
    }

    private static string GetAvailableFilePath(string destinationFolder, string sourceFileName)
    {
        var sanitizedFileName = SanitizeFileName(sourceFileName);
        var baseName = Path.GetFileNameWithoutExtension(sanitizedFileName);
        var extension = Path.GetExtension(sanitizedFileName);
        var candidatePath = Path.Combine(destinationFolder, sanitizedFileName);
        var suffix = 1;

        while (File.Exists(candidatePath))
        {
            candidatePath = Path.Combine(destinationFolder, $"{baseName} ({suffix}){extension}");
            suffix++;
        }

        return candidatePath;
    }

    private static string SanitizeFileName(string fileName)
    {
        var builder = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(InvalidFileNameChars.Contains(character) ? '-' : character);
        }

        return builder.ToString();
    }

    private static string SanitizeFolderSegment(string segment)
    {
        var builder = new StringBuilder(segment.Length);
        foreach (var character in segment)
        {
            if (InvalidFileNameChars.Contains(character))
            {
                builder.Append('-');
                continue;
            }

            builder.Append(character switch
            {
                ':' or '/' or '\\' => '-',
                _ => character
            });
        }

        return builder.ToString().Trim();
    }
}
