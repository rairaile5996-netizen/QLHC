using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocModel = DocumentManagement.Models; // Alias for Models

namespace DocumentManagement.Services;

public class DocumentTemplateService
{
    private const string TEMPLATE_FOLDER_NAME = "Templates";

    public static List<string> GetAvailableTemplates()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement");
        var templatePath = Path.Combine(appDataPath, TEMPLATE_FOLDER_NAME);

        if (!Directory.Exists(templatePath))
        {
            Directory.CreateDirectory(templatePath);
            CreateSampleTemplate(Path.Combine(templatePath, "Mau_De_Nghi_Thanh_Toan.docx"));
        }

        var files = Directory.GetFiles(templatePath, "*.docx")
                        .Select(Path.GetFileName)
                        .Where(x => !string.IsNullOrEmpty(x))
                        .ToList();

        // Fix potential nullability warning manually if needed, or Select ensures non-null but GetFileName can return null path? No, path is valid. 
        // But Select returns string? which might cause warning.
        // Let's rely on Where to filter nulls.
        return files!;
    }


    public static string GetTemplateFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement", TEMPLATE_FOLDER_NAME);
    }

    public static async Task<string> GenerateDocumentAsync(DocModel.Document document, string templateName, Dictionary<string, string> extraData)
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement");
        var templatePath = Path.Combine(appDataPath, TEMPLATE_FOLDER_NAME, templateName);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Không tìm thấy template: {templateName}");
        }

        if (string.IsNullOrEmpty(document.FolderPath))
        {
            throw new InvalidOperationException("Hồ sơ chưa có thư mục lưu trữ.");
        }

        // Tạo thư mục đầu ra: [Thư mục hồ sơ]/GeneratedDocuments
        var outputFolder = Path.Combine(document.FolderPath, "GeneratedDocuments");
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        // Tên file output: [yyyyMMdd]_[TenTemplate]_[SoHD].docx
        var fileName = $"{DateTime.Now:yyyyMMdd}_{Path.GetFileNameWithoutExtension(templateName)}_{document.ContractNumber.Replace("/", "-")}.docx";
        var outputPath = Path.Combine(outputFolder, fileName);

        // Copy template sang output
        File.Copy(templatePath, outputPath, true);

        // Chuẩn bị dữ liệu merge
        var mergeData = new Dictionary<string, string>(extraData)
        {
            { "SoHD", document.ContractNumber ?? "" },
            { "TenDuAn", document.ProjectName ?? "" },
            { "HangMuc", document.ItemName ?? "" },
            { "NgayKy", document.SignDate.ToString("dd/MM/yyyy") },
            { "GiaTriHD", document.ContractValue.ToString("N0") },
            { "NguoiLap", Environment.UserName }, // Hoặc lấy từ settings
            { "NgayHienTai", DateTime.Now.ToString("dd/MM/yyyy") }
        };

        // Thực hiện thay thế
        await Task.Run(() => ProcessWordDocument(outputPath, mergeData));

        return outputPath;
    }

    private static void ProcessWordDocument(string filePath, Dictionary<string, string> replacements)
    {
        using var wordDoc = WordprocessingDocument.Open(filePath, true);
        var body = wordDoc.MainDocumentPart?.Document.Body;
        if (body == null) return;

        var textElements = body.Descendants<Text>().ToList();

        foreach (var text in textElements)
        {
            foreach (var kvp in replacements)
            {
                var placeholder = $"{{{{{kvp.Key}}}}}"; // Format {{Key}}
                if (text.Text.Contains(placeholder))
                {
                    text.Text = text.Text.Replace(placeholder, kvp.Value);
                }
            }
        }

        // Save handled by Dispose
    }

    private static void CreateSampleTemplate(string filePath)
    {
        try
        {
            using var wordDoc = WordprocessingDocument.Create(filePath, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new Body());

            var para = body.AppendChild(new Paragraph());
            var run = para.AppendChild(new Run());
            run.AppendChild(new Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM"));

            body.AppendChild(new Paragraph(new Run(new Text("Độc lập - Tự do - Hạnh phúc"))));
            body.AppendChild(new Paragraph(new Run(new Text(""))));
            body.AppendChild(new Paragraph(new Run(new Text("GIẤY ĐỀ NGHỊ THANH TOÁN"))));
            body.AppendChild(new Paragraph(new Run(new Text(""))));

            body.AppendChild(new Paragraph(new Run(new Text("Kính gửi: Ban Giám Đốc"))));
            body.AppendChild(new Paragraph(new Run(new Text(""))));
            body.AppendChild(new Paragraph(new Run(new Text("Căn cứ Hợp đồng số: {{SoHD}}"))));
            body.AppendChild(new Paragraph(new Run(new Text("Dự án: {{TenDuAn}}"))));
            body.AppendChild(new Paragraph(new Run(new Text("Hạng mục: {{HangMuc}}"))));
            body.AppendChild(new Paragraph(new Run(new Text("Giá trị hợp đồng: {{GiaTriHD}} VNĐ"))));
            body.AppendChild(new Paragraph(new Run(new Text(""))));
            body.AppendChild(new Paragraph(new Run(new Text("Đề nghị thanh toán số tiền: {{GiaTriThanhToan}} VNĐ"))));
            body.AppendChild(new Paragraph(new Run(new Text("Lý do: {{KhoanThanhToan}}")))); // VD: Thanh toán đợt 1
            body.AppendChild(new Paragraph(new Run(new Text(""))));
            body.AppendChild(new Paragraph(new Run(new Text("Ngày: {{NgayHienTai}}"))));
            body.AppendChild(new Paragraph(new Run(new Text("Người lập: {{NguoiLap}}"))));

            mainPart.Document.Save();
        }
        catch { }
    }
}
