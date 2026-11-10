using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentManagement.Models;
using DocumentManagement.Utilities;
using ContractDocument = DocumentManagement.Models.Document;

namespace DocumentManagement.Services;

public sealed class OtherQualityDocumentExportService
{
    public Task<string> GenerateAsync(
        ContractDocument contract,
        CvxdInfo info,
        CvxdGlobalConfig config,
        string templatePath,
        string outputPath,
        DateTime? creationDate = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.Copy(templatePath, outputPath, true);

        var replacements = PlaceholderRegistry.BuildCvxd(contract, info, config);
        var dateText = creationDate.HasValue
            ? creationDate.Value.ToString("dd/MM/yyyy")
            : "Ngày ...... tháng ...... năm ......";
        replacements["NgayTaoHoSo"] = dateText;
        replacements["NgayLapHoSo"] = dateText;
        replacements["NgayTao"] = dateText;
        replacements["NgayLap"] = dateText;
        replacements["NgayTao_Day"] = creationDate?.Day.ToString("00") ?? "......";
        replacements["NgayTao_Month"] = creationDate?.Month.ToString("00") ?? "......";
        replacements["NgayTao_Year"] = creationDate?.Year.ToString() ?? "......";
        using var document = WordprocessingDocument.Open(outputPath, true);
        if (document.MainDocumentPart is not { } mainPart)
            throw new InvalidDataException("File mẫu không có phần nội dung Word hợp lệ.");

        ReplaceInElement(mainPart.Document.Body, replacements);
        foreach (var header in mainPart.HeaderParts)
            ReplaceInElement(header.Header, replacements);
        foreach (var footer in mainPart.FooterParts)
            ReplaceInElement(footer.Footer, replacements);

        mainPart.Document.Save();
        return Task.FromResult(outputPath);
    }

    private static void ReplaceInElement(OpenXmlElement? element, IReadOnlyDictionary<string, string> replacements)
    {
        if (element == null) return;

        foreach (var paragraph in element.Descendants<Paragraph>().ToList())
        {
            var texts = paragraph.Descendants<Text>().ToList();
            if (texts.Count == 0) continue;

            var content = string.Concat(texts.Select(text => text.Text));
            var replaced = ReplaceTokens(content, replacements);
            if (replaced == content) continue;

            texts[0].Text = replaced;
            for (var index = 1; index < texts.Count; index++)
                texts[index].Text = string.Empty;
        }
    }

    private static string ReplaceTokens(string text, IReadOnlyDictionary<string, string> replacements)
    {
        var result = text;
        foreach (var replacement in replacements)
        {
            if (string.IsNullOrWhiteSpace(replacement.Key)) continue;
            var value = replacement.Value ?? string.Empty;
            result = result.Replace($"{{{replacement.Key}}}", value, StringComparison.OrdinalIgnoreCase);
            result = result.Replace($"[{replacement.Key}]", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
