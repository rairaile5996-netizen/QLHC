using System.Diagnostics;
using System.IO;
using System.Text;
using DocumentManagement.Models;

namespace DocumentManagement.Services;

/// <summary>
/// Shared service for AI Chat functionality - eliminates duplicate code between ViewModels
/// </summary>
public class AiChatService(AIService aiService)
{
    private readonly AIService _aiService = aiService;

    /// <summary>
    /// Build prompt and send chat message to AI API
    /// </summary>
    public async Task<string> SendChatAsync(
        string userMessage,
        IEnumerable<ChatMessage> chatHistory,
        Document? selectedDocument,
        IEnumerable<Document> allDocuments)
    {
        var context = await BuildDocumentsContextAsync(selectedDocument, allDocuments);

        // Build Chat History (last 10 messages)
        var historyBuilder = new StringBuilder();
        var historyMessages = chatHistory.TakeLast(10);
        if (historyMessages.Any())
        {
            historyBuilder.AppendLine("LỊCH SỬ CHAT (Tham khảo để hiểu ngữ cảnh câu hỏi tiếp theo):");
            foreach (var msg in historyMessages)
            {
                historyBuilder.AppendLine($"{(msg.IsUser ? "User" : "AI")}: {msg.Content}");
            }
            historyBuilder.AppendLine("--- KẾT THÚC LỊCH SỬ ---");
        }

        var prompt = $@"Bạn là trợ lý AI chuyên nghiệp giúp quản lý hồ sơ hợp đồng xây dựng.
NHIỆM VỤ: Trả lời câu hỏi của người dùng dựa trên Dữ liệu cung cấp và Lịch sử chat.

DỮ LIÊU CUNG CẤP:
{context}

{historyBuilder}

CÂU HỎI HIỆN TẠI CỦA USER: {userMessage}

YÊU CẦU TRẢ LỜI:
1. Trả lời bằng tiếng Việt, văn phong chuyên nghiệp nhưng thân thiện.
2. Ngắn gọn, đi thẳng vào vấn đề. Dùng định dạng Markdown (in đậm, danh sách) để dễ đọc.
3. Nếu câu hỏi liên quan đến hợp đồng đang chọn (Selected), hãy dùng chi tiết trong phần 'NỘI DUNG FILE'.
4. Nếu không tìm thấy thông tin, hãy nói rõ 'Không tìm thấy thông tin trong hồ sơ này'.";

        return await _aiService.SendChatAsync(prompt);
    }

    /// <summary>
    /// Build context string from documents for AI prompt
    /// </summary>
    public static async Task<string> BuildDocumentsContextAsync(Document? selectedDocument, IEnumerable<Document> documents)
    {
        var sb = new StringBuilder();

        if (selectedDocument != null)
        {
            sb.AppendLine("=== HỒ SƠ ĐANG ĐƯỢC CHỌN (Ưu tiên trả lời về hồ sơ này) ===");
            await AppendDocumentDetailsAsync(sb, selectedDocument, true);

            sb.AppendLine("\n=== DANH SÁCH CÁC HỒ SƠ KHÁC TRONG HỆ THỐNG (Thông tin tóm tắt) ===");
            foreach (var doc in documents.Where(d => d != selectedDocument))
            {
                sb.AppendLine($"- Số HĐ: {doc.ContractNumber} | Dự án: {doc.ProjectName} | Giá trị: {doc.ContractValue:N0}");
            }
        }
        else
        {
            sb.AppendLine("=== DANH SÁCH TẤT CẢ HỒ SƠ ===");
            sb.AppendLine("(Người dùng chưa chọn hồ sơ cụ thể. Nếu câu hỏi yêu cầu chi tiết về một hồ sơ, hãy nhắc họ chọn hồ sơ đó từ danh sách)");
            foreach (var doc in documents)
            {
                await AppendDocumentDetailsAsync(sb, doc, false);
            }
        }

        return sb.ToString();
    }

    private static async Task AppendDocumentDetailsAsync(StringBuilder sb, Document doc, bool includeFiles)
    {
        sb.AppendLine($"---");
        sb.AppendLine($"Số HĐ: {doc.ContractNumber}");
        sb.AppendLine($"Tên dự án: {doc.ProjectName}");
        sb.AppendLine($"Hạng mục: {doc.ItemName}");
        sb.AppendLine($"Ngày ký: {doc.SignDate:dd/MM/yyyy}");
        sb.AppendLine($"Giá trị hợp đồng: {doc.ContractValue:N0} đ");

        if (includeFiles)
        {
            sb.AppendLine($"Tổng tạm ứng: {doc.TotalAdvance:N0} đ");
            sb.AppendLine($"Tổng thanh toán: {doc.TotalPayment:N0} đ");
            sb.AppendLine($"Giá trị quyết toán: {doc.SettlementValue:N0} đ");
            sb.AppendLine($"Giữ lại bảo hành: {doc.WarrantyValue:N0} đ");
            sb.AppendLine($"Còn lại thanh toán: {doc.RemainingPaymentValue:N0} đ");
            if (doc.WarrantyExpiryDate.HasValue)
                sb.AppendLine($"Hết hạn bảo hành: {doc.WarrantyExpiryDate:dd/MM/yyyy}");

            if (doc.Appendices is { Count: > 0 })
            {
                sb.AppendLine($"Phụ lục ({doc.Appendices.Count}):");
                foreach (var app in doc.Appendices)
                {
                    sb.AppendLine($"  - {app.Description}: {app.Value:N0} đ (Ngày: {app.SignDate:dd/MM/yyyy})");
                }
            }

            // Đọc nội dung file Word
            if (!string.IsNullOrEmpty(doc.FolderPath) && Directory.Exists(doc.FolderPath))
            {
                var fileContent = await ReadDocumentFilesAsync(doc.FolderPath);
                if (!string.IsNullOrEmpty(fileContent))
                {
                    sb.AppendLine($"\n[NỘI DUNG FILE ĐÍNH KÈM CỦA {doc.ContractNumber}]:");
                    sb.AppendLine(fileContent);
                }
            }
        }
    }

    private static async Task<string> ReadDocumentFilesAsync(string folderPath)
    {
        var sb = new StringBuilder();

        try
        {
            var inputFolder = Path.Combine(folderPath, "1.INPUT");
            if (Directory.Exists(inputFolder))
            {
                var wordFiles = Directory.GetFiles(inputFolder, "*.docx", SearchOption.TopDirectoryOnly)
                    .Take(3);

                foreach (var file in wordFiles)
                {
                    var content = await Task.Run(() => ReadWordFileContent(file));
                    if (!string.IsNullOrEmpty(content))
                    {
                        sb.AppendLine($"[File: {Path.GetFileName(file)}]");
                        sb.AppendLine(content.Length > 2000 ? content[..2000] + "..." : content);
                        sb.AppendLine();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi đọc file: {ex.Message}");
        }

        return sb.ToString();
    }

    private static string ReadWordFileContent(string filePath)
    {
        try
        {
            using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var element in body.ChildElements)
            {
                if (element is DocumentFormat.OpenXml.Wordprocessing.Paragraph para)
                {
                    var text = para.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(text))
                        sb.AppendLine(text);
                }
                else if (element is DocumentFormat.OpenXml.Wordprocessing.Table table)
                {
                    foreach (var row in table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
                    {
                        List<string> cells = [];
                        foreach (var cell in row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
                        {
                            var cellText = cell.InnerText?.Trim();
                            if (!string.IsNullOrEmpty(cellText))
                                cells.Add(cellText);
                        }
                        if (cells.Count > 0)
                            sb.AppendLine(string.Join(" | ", cells));
                    }
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi đọc file Word: {ex.Message}");
            return string.Empty;
        }
    }
}
