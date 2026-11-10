using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Win32;
using DocumentManagement.Models;
using DocumentManagement.Utilities;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Collections.Generic;
using System.Text.Json; // Added for JsonSerializerOptions
using ClosedXML.Excel;

namespace DocumentManagement.Services;

public sealed record CvxdExportProgress(string Message, double Percent);

public sealed class CvxdExportPreflightResult
{
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public int SelectedCount { get; set; }
    public int ChecklistCount { get; set; }
    public bool CanExport => Errors.Count == 0;

    public string ToMessage()
    {
        var lines = new List<string>
        {
            $"Biên bản đã chọn: {SelectedCount}",
            $"Checklist cần chèn: {ChecklistCount}"
        };

        if (Errors.Count > 0)
        {
            lines.Add("");
            lines.Add("Lỗi cần xử lý:");
            lines.AddRange(Errors.Select(e => "- " + e));
        }

        if (Warnings.Count > 0)
        {
            lines.Add("");
            lines.Add("Cảnh báo:");
            lines.AddRange(Warnings.Select(w => "- " + w));
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public class CvxdExportService
{
    private readonly string _basePath;
    private const string ItemStartMarkerPrefix = "__CVXD_ITEM_START_";
    private const string ItemEndMarkerPrefix = "__CVXD_ITEM_END_";
    private static readonly object LogLock = new();

    public CvxdExportService()
    {
        _basePath = AppDomain.CurrentDomain.BaseDirectory;
    }

    public string ExportToExcel(DocumentManagement.Models.Document doc, CvxdInfo info, List<CvxdItem> items, string outputPath)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("Không có dữ liệu CVXD để xuất Excel.");
        }

        var customColumns = info.CustomColumns
            .Select(c => c.Name?.Trim() ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("CVXD");

        var headers = new List<string>
        {
            "Chọn",
            "STT",
            "Loại NT",
            "Nội dung Nghiệm thu",
            "Ngày NT",
            "Bắt đầu",
            "Kết thúc",
            "Số Checklist",
            "Số PDVT",
            "Loại BB",
            "Tên NT",
            "TCNT"
        };
        headers.AddRange(customColumns);

        for (int col = 0; col < headers.Count; col++)
        {
            worksheet.Cell(1, col + 1).Value = headers[col];
        }

        for (int rowIndex = 0; rowIndex < items.Count; rowIndex++)
        {
            var item = items[rowIndex];
            int row = rowIndex + 2;
            int col = 1;

            worksheet.Cell(row, col++).Value = item.IsSelected ? "Có" : "Không";
            worksheet.Cell(row, col++).Value = item.Stt;
            worksheet.Cell(row, col++).Value = item.LoaiNt;
            worksheet.Cell(row, col++).Value = item.NoiDung;
            worksheet.Cell(row, col++).Value = item.NgayNt;
            worksheet.Cell(row, col++).Value = item.BatDau;
            worksheet.Cell(row, col++).Value = item.KetThuc;
            worksheet.Cell(row, col++).Value = item.SoChecklist;
            worksheet.Cell(row, col++).Value = item.SoPdvt;
            worksheet.Cell(row, col++).Value = item.LoaiBb;
            worksheet.Cell(row, col++).Value = item.TenNt;
            worksheet.Cell(row, col++).Value = item.Tcnt;

            foreach (var customColumn in customColumns)
            {
                var value = item.CustomFields
                    .FirstOrDefault(f => string.Equals(f.Name, customColumn, StringComparison.OrdinalIgnoreCase))
                    ?.Value ?? string.Empty;
                worksheet.Cell(row, col++).Value = value;
            }
        }

        var usedRange = worksheet.Range(1, 1, items.Count + 1, headers.Count);
        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        usedRange.Style.Alignment.WrapText = true;

        var headerRange = worksheet.Range(1, 1, 1, headers.Count);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        worksheet.SheetView.FreezeRows(1);
        var defaultWidths = new double[] { 8, 8, 12, 42, 14, 12, 12, 14, 18, 24, 24, 18 };
        for (int i = 0; i < headers.Count; i++)
        {
            worksheet.Column(i + 1).Width = i < defaultWidths.Length ? defaultWidths[i] : 22;
        }
        worksheet.Row(1).Height = 24;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? _basePath);
        workbook.SaveAs(outputPath);
        ExportedFileRegistry.Record(doc.FolderPath, outputPath, "CVXD");
        return outputPath;
    }

    private string GetTemplatePathForType(string? loaiNt, string? customRootPath)
    {
        // 1. Xác định thư mục cha (Ưu tiên folder cài đặt, nếu không có dùng mặc định)
        string parentRoot = (!string.IsNullOrEmpty(customRootPath) && Directory.Exists(customRootPath))
            ? customRootPath
            : Path.Combine(_basePath, "Templates");

        // 2. Thư mục con CVXD (module hiện tại)
        string cvxdFolder = Path.Combine(parentRoot, "CVXD");

        loaiNt = loaiNt?.Trim();

        // CVXD and LĐTTB have separate Word templates.
        if (IsLdttbType(loaiNt))
        {
            return Path.Combine(cvxdFolder, "LĐTTB_Template.docx");
        }

        if (string.IsNullOrEmpty(loaiNt) || IsCvxdType(loaiNt))
        {
            return Path.Combine(cvxdFolder, "CVXD_Template.docx");
        }

        // 4. Các loại khác (TA, TK, LĐKT...) phải dùng đúng Template của nó
        return Path.Combine(cvxdFolder, $"{loaiNt}_Template.docx");
    }

    public CvxdExportPreflightResult ValidateExport(CvxdGlobalConfig globalConfig, List<CvxdItem> items, bool exportAll, string? profile = null)
    {
        var selectedItems = exportAll ? items : [.. items.Where(i => i.IsSelected)];
        var result = new CvxdExportPreflightResult
        {
            SelectedCount = selectedItems.Count,
            ChecklistCount = selectedItems.Count(NeedsChecklist)
        };

        if (selectedItems.Count == 0)
        {
            result.Errors.Add("Vui lòng tích chọn ít nhất một biên bản để xuất.");
            return result;
        }

        foreach (var loaiNt in selectedItems.Where(i => i.LoaiNt != "CL").Select(i => i.LoaiNt?.Trim() ?? "CVXD").Distinct())
        {
            var templatePath = GetTemplatePathForType(loaiNt, TemplateProfileResolver.GetTemplateRoot(globalConfig, profile));
            if (!File.Exists(templatePath))
                result.Errors.Add($"Thiếu template cho loại NT '{loaiNt}': {templatePath}");
        }

        var checklistFolder = GetChecklistFolder(globalConfig, profile);
        foreach (var item in selectedItems.Where(NeedsChecklist))
        {
            var checklistPath = FindChecklistExcel(item, checklistFolder);
            if (string.IsNullOrEmpty(checklistPath))
                result.Errors.Add($"Thiếu checklist cho dòng '{item.TenNt}' ({item.LoaiBb}). Thư mục: {checklistFolder}");
        }

        return result;
    }

    public async Task<string?> ExportToWordAsync(DocumentManagement.Models.Document doc, CvxdInfo info, CvxdGlobalConfig globalConfig, List<CvxdItem> items, bool exportAll, IProgress<CvxdExportProgress>? progress = null)
    {
        try
        {
            List<CvxdItem> selectedItems = exportAll ? items : [.. items.Where(i => i.IsSelected)];
            var preflight = ValidateExport(globalConfig, items, exportAll, info.TemplateProfile);
            if (!preflight.CanExport)
            {
                throw new InvalidOperationException(preflight.ToMessage());
            }

            if (selectedItems.Count == 0)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Vui lòng tích chọn ít nhất một biên bản ở cột đầu tiên để xuất!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
                return null;
            }

            progress?.Report(new CvxdExportProgress("Đang chuẩn bị template Word...", 5));
            string effectiveTemplateRoot = TemplateProfileResolver.GetTemplateRoot(globalConfig, info.TemplateProfile);
            string baseTemplatePath = GetTemplatePathForType(selectedItems[0].LoaiNt, effectiveTemplateRoot);
            if (!File.Exists(baseTemplatePath))
            {
                throw new FileNotFoundException($"Không tìm thấy file template: {Path.GetFileName(baseTemplatePath)}\n\nLoại NT: {selectedItems[0].LoaiNt}\nĐường dẫn: {Path.GetDirectoryName(baseTemplatePath)}\n\nVui lòng tạo file template này trong thư mục Templates\\CVXD\\ hoặc kiểm tra lại cấu hình đường dẫn template.");
            }

            string outputDir = Path.Combine(doc.FolderPath ?? _basePath, "3.HSCL", "CVXD");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            string fileName = $"CVXD_{doc.ContractNumber.Replace("/", "-")}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            string outputPath = Path.Combine(outputDir, fileName);

            // Copy template sang file mới
            File.Copy(baseTemplatePath, outputPath, true);

            progress?.Report(new CvxdExportProgress("Đang ghép nội dung biên bản...", 15));
            await Task.Run(() =>
            {
                using var wordDoc = WordprocessingDocument.Open(outputPath, true);
                MainDocumentPart mainPart = wordDoc.MainDocumentPart!;
                Body body = mainPart.Document.Body!;

                // Dictionary để cache các thành phần mẫu từ các file template khác nhau
                var templateCache = new Dictionary<string, List<OpenXmlElement>>();

                // Lưu lại SectionProperties của base document để giữ nguyên định dạng trang (margin, orientation)
                var baseSectPr = body.Elements<SectionProperties>().LastOrDefault()?.CloneNode(true);

                // Xóa nội dung hiện tại để bắt đầu chèn theo vòng lặp
                body.RemoveAllChildren();

                // Tính STT_BB theo từng loại NT (CVXD và LĐTTB đếm riêng)
                // Keep sequence numbers based on the full CVXD list. Exporting one checked row
                // must not restart its YCNT/NT number from 01.
                var calculatedStringsMap = CalculateSttBbForExport(items, selectedItems, info.KyHieuNhaThau);

                for (int i = 0; i < selectedItems.Count; i++)
                {
                    progress?.Report(new CvxdExportProgress($"Đang xử lý biên bản {i + 1}/{selectedItems.Count}...", 15 + ((i + 1) * 55.0 / selectedItems.Count)));
                    try
                    {
                        var item = selectedItems[i];
                        var calculated = calculatedStringsMap[i];
                        body.AppendChild(CreateMarkerParagraph(GetItemStartMarker(i)));

                            // Quy tắc chèn: 
                            // 1. Dòng chính (không phải CL): Chèn Word.
                            // 2. Chèn Checklist khi: (là dòng CL) HOẶC (dòng chính có Số Checklist == 1).
                            bool shouldInsertWord = item.LoaiNt != "CL";
                            bool shouldInsertChecklist = (item.LoaiNt == "CL") || (item.LoaiNt != "CL" && item.SoChecklist == "1");

                            if (shouldInsertWord)
                            {
                                // Xác định template cho item này
                                string itemTemplatePath = GetTemplatePathForType(item.LoaiNt, effectiveTemplateRoot);
                                if (!templateCache.ContainsKey(itemTemplatePath))
                                {
                                    if (File.Exists(itemTemplatePath))
                                    {
                                        using var tempDoc = WordprocessingDocument.Open(itemTemplatePath, false);
                                        // Loại bỏ SectionProperties khi clone để tránh làm hỏng cấu trúc file Word (chỉ được có 1 SectPr ở cuối Body)
                                        templateCache[itemTemplatePath] = [.. tempDoc.MainDocumentPart!.Document.Body!
                                            .ChildElements.Where(e => e is not SectionProperties).Select(e => e.CloneNode(true))];
                                    }
                                    else
                                    {
                                        // Không tìm thấy template → Báo lỗi rõ ràng cho người dùng
                                        string templateFileName = Path.GetFileName(itemTemplatePath);
                                        string templateFolder = Path.GetDirectoryName(itemTemplatePath) ?? "";
                                        throw new FileNotFoundException(
                                            $"Không tìm thấy file template: {templateFileName}\n\n" +
                                            $"Loại NT: {item.LoaiNt}\n" +
                                            $"Đường dẫn: {templateFolder}\n\n" +
                                            $"Vui lòng tạo file template này trong thư mục Templates\\CVXD\\ " +
                                            $"hoặc kiểm tra lại cấu hình đường dẫn template."
                                        );
                                    }
                                }

                                var templateElements = templateCache[itemTemplatePath];
                                foreach (var element in templateElements)
                                {
                                    var clonedElement = body.AppendChild(element.CloneNode(true));

                                    var replacements = GetGlobalReplacements(doc, info, globalConfig, item);
                                    var itemReplacements = GetItemReplacements(item, calculated, globalConfig.GlobalStandards?.ToList(), doc, info);
                                    foreach (var kvp in itemReplacements) replacements[kvp.Key] = kvp.Value;

                                    ReplacePlaceholders(clonedElement, replacements);
                                }

                                // Nếu dòng chính có chèn checklist, tự động chèn vào trang tiếp theo (không dùng placeholder {26} nữa)
                                if (shouldInsertChecklist)
                                {
                                    body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
                                    body.AppendChild(new Paragraph(new Run(new Text($"{{26_{i}}}"))));
                                }
                            }
                            else
                            {
                                // Dòng CL: Chỉ chèn mã {26_i} vào một đoạn văn mới
                                var para = body.AppendChild(new Paragraph(new Run(new Text($"{{26_{i}}}"))));
                            }

                            body.AppendChild(CreateMarkerParagraph(GetItemEndMarker(i)));

                            // Chèn dấu ngắt trang nếu không phải item cuối cùng
                            if (i < selectedItems.Count - 1)
                            {
                                body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Lỗi khi xử lý dòng {i + 1} (Loại NT: {selectedItems[i].LoaiNt}, Nội dung: {selectedItems[i].TenNt}): {ex.Message}", ex);
                        }
                    }

                    // Khôi phục lại SectionProperties cho toàn bộ tài liệu ở cuối cùng
                    if (baseSectPr != null)
                    {
                        body.AppendChild(baseSectPr);
                    }

                    mainPart.Document.Save();

                    // 3. Thay thế placeholders trong Header và Footer (nếu có)
                    // Lưu ý: Replacements này là của Global (chung) vì Header/Footer thường cố định cho cả file
                    foreach (var headerPart in mainPart.HeaderParts)
                    {
                        var globalReplacements = GetGlobalReplacements(doc, info, globalConfig);
                        ReplacePlaceholders(headerPart.Header, globalReplacements);
                        headerPart.Header.Save();
                    }
                    foreach (var footerPart in mainPart.FooterParts)
                    {
                        var globalReplacements = GetGlobalReplacements(doc, info, globalConfig);
                        ReplacePlaceholders(footerPart.Footer, globalReplacements);
                        footerPart.Footer.Save();
                    }
            });

            // 120. Dùng Interop để chèn OLE thực thụ vào các vị trí {26_i}
            // Kiểm tra nếu có bất kỳ item nào cần chèn OLE (CL hoặc main row có SoChecklist=1)
            bool isOleExport = selectedItems.Exists(item => 
                item.LoaiNt == "CL" || (item.LoaiNt != "CL" && item.SoChecklist == "1"));
            if (isOleExport)
            {
                progress?.Report(new CvxdExportProgress("Đang chèn checklist Excel vào Word...", 75));
                await ProcessOleWithInterop(outputPath, doc, info, globalConfig, items, selectedItems, progress);
            }

            ExportedFileRegistry.Record(doc.FolderPath, outputPath, "CVXD");
            // Mở file sau khi hoàn tất
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể mở file tự động: {ex.Message}\nĐường dẫn: {outputPath}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
            return outputPath;
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Đã xảy ra lỗi khi xuất file Word: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            });
            throw;
        }
    }

    private static void LogExportWarning(string message, Exception? ex = null)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DocumentManagement",
                "Logs");
            Directory.CreateDirectory(logDir);

            var logPath = Path.Combine(logDir, "cvxd-export.log");
            var detail = ex == null ? string.Empty : $" | {ex.GetType().Name}: {ex.Message}";
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}{detail}{Environment.NewLine}";

            lock (LogLock)
            {
                File.AppendAllText(logPath, line);
            }
        }
        catch
        {
            // Logging must never interrupt export.
        }
    }

    private static void ReplacePlaceholders(OpenXmlElement element, Dictionary<string, string> replacements)
    {
        // Thực hiện thay thế theo từng đoạn văn (Paragraph) để tránh làm hỏng cấu trúc các block lớn (như Table)
        var paragraphs = element.Descendants<Paragraph>().ToList();
        if (element is Paragraph pSelf)
        {
            paragraphs.Add(pSelf);
        }

        foreach (var p in paragraphs)
        {
            foreach (var kvp in replacements)
            {
                if (string.IsNullOrEmpty(kvp.Key)) continue;
                ReplaceWordText(p, kvp.Key, kvp.Value ?? "");
            }
        }

        // Xử lý cả các đối tượng đơn lẻ nằm ngoài Paragraph (nếu có)
        foreach (var kvp in replacements)
        {
            if (string.IsNullOrEmpty(kvp.Key)) continue;
            // Chỉ chạy cho các Text node trực tiếp không thuộc Paragraph nào ở trên (hiếm gặp)
            var directTexts = element.ChildElements.OfType<Text>().ToList();
            foreach (var t in directTexts)
            {
                if (t.Text.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase)) t.Text = t.Text.Replace(kvp.Key, kvp.Value ?? "", StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static void ReplaceWordText(OpenXmlElement element, string find, string replace)
    {
        if (string.IsNullOrEmpty(find)) return;
        
        var texts = element.Descendants<Text>().ToList();
        if (texts.Count == 0) return;

        // 1. Trường hợp tag nằm gọn trong 1 thẻ Text đơn lẻ (Phổ biến nhất)
        bool replaced = false;
        foreach (var t in texts)
        {
            if (t.Text.Contains(find, StringComparison.OrdinalIgnoreCase))
            {
                ApplyTextWithBreaks(t, t.Text.Replace(find, replace, StringComparison.OrdinalIgnoreCase));
                replaced = true;
            }
        }
        if (replaced) return;

        // 2. Trường hợp Tag bị Word chia nhỏ Run (ví dụ: { và TCNT và })
        // Chỉ xử lý gộp nếu element là Paragraph để đảm bảo an toàn
        if (element is Paragraph || element.Parent is Paragraph)
        {
            string fullText = string.Concat(texts.Select(t => t.Text));
            if (fullText.Contains(find, StringComparison.OrdinalIgnoreCase))
            {
                string newFullText = fullText.Replace(find, replace, StringComparison.OrdinalIgnoreCase);
                ApplyTextWithBreaks(texts[0], newFullText);
                // Các thẻ sau để trống, giữ nguyên số lượng node tránh crash
                for (int i = 1; i < texts.Count; i++) texts[i].Text = "";
            }
        }
    }

    private static void ApplyTextWithBreaks(Text textElement, string fullText)
    {
        if (!fullText.Contains('\n'))
        {
            textElement.Text = fullText;
            return;
        }

        var run = textElement.Parent;
        if (run == null)
        {
            textElement.Text = fullText.Replace("\r", "").Replace("\n", " ");
            return;
        }

        var lines = fullText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        if (run.Parent is Paragraph paragraph)
        {
            // Chiến lược nhân bản Đoạn văn (Paragraph) để bảo toàn FirstLineIndent, LeftIndent...
            textElement.Text = lines[0];
            Paragraph currentParagraph = paragraph;

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue; // Bỏ qua dòng trống

                Paragraph newPara = (Paragraph)paragraph.CloneNode(true);
                var newTexts = newPara.Descendants<Text>().ToList();
                
                if (newTexts.Count > 0)
                {
                    newTexts[0].Text = lines[i];
                    for (int j = 1; j < newTexts.Count; j++) newTexts[j].Text = "";
                }
                
                currentParagraph.InsertAfterSelf(newPara);
                currentParagraph = newPara;
            }
        }
        else
        {
            // Fallback: dùng <w:br/> nếu không ở trong Paragraph
            textElement.Text = lines[0];
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var breakElement = new Break();
                var newTextElement = new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve };
                run.InsertAfter(newTextElement, textElement);
                run.InsertAfter(breakElement, textElement);
                textElement = newTextElement;
            }
        }
    }

    private static Paragraph CreateMarkerParagraph(string marker)
    {
        return new Paragraph(
            new Run(
                new RunProperties(new Vanish()),
                new Text(marker) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static string GetItemStartMarker(int index) => $"{ItemStartMarkerPrefix}{index}__";

    private static string GetItemEndMarker(int index) => $"{ItemEndMarkerPrefix}{index}__";

    private async Task ProcessOleWithInterop(string wordPath, Models.Document doc, CvxdInfo info, CvxdGlobalConfig globalConfig, List<CvxdItem> allItems, List<CvxdItem> items, IProgress<CvxdExportProgress>? progress)
    {
        // Sử dụng TaskCompletionSource để đợi STA thread một cách bất đồng bộ (không block UI)
        var tcs = new TaskCompletionSource<bool>();
        Exception? interopError = null;

        var staThread = new Thread(() =>
        {
            dynamic? wordApp = null;
            dynamic? excelApp = null;
            try
            {
                wordApp = OfficeInteropSession.CreateWordApplication();
                excelApp = OfficeInteropSession.CreateExcelApplication();
                if (wordApp == null || excelApp == null)
                {
                    interopError = new Exception("Không tìm thấy Word hoặc Excel trên máy. Vui lòng cài đặt Microsoft Office.");
                    tcs.SetResult(false);
                    return;
                }

                if (wordApp == null || excelApp == null) return;

                dynamic wordDoc = wordApp!.Documents.Open(wordPath);
                try { wordApp.Visible = false; } catch { }
                try { wordApp.ShowWindowsInTaskbar = false; } catch { }
                OfficeInteropSession.HideWindow(wordApp);
                var calculatedStringsMap = CalculateSttBbForExport(allItems, items, info.KyHieuNhaThau);
                // actualChecklistCount là biến đếm toàn cục cho checklist trong file (không dùng nữa, dùng theo map)

                // KHẮC PHỤC LỖI OLE: Chèn trực tiếp vào document chính, save+reopen giữa các lần chèn
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var itemPercent = 75 + ((i + 1) * 18.0 / items.Count);
                    progress?.Report(new CvxdExportProgress($"Đang chèn checklist {i + 1}/{items.Count}...", itemPercent));
                    
                    // Xác định đường dẫn checklist từ GlobalConfig hoặc mặc định
                    string checklistFolder = TemplateProfileResolver.GetChecklistRoot(globalConfig, info.TemplateProfile);
                    
                    string? xlPath = FindChecklistExcel(item, checklistFolder);
                    bool skipChecklist = string.IsNullOrEmpty(xlPath) 
                        || item.SoChecklist == "0" 
                        || string.IsNullOrWhiteSpace(item.SoChecklist);
                    
                    if (skipChecklist)
                    {
                        // Không có checklist → xóa placeholder {26_i}
                        dynamic findClean = wordDoc.Content.Find;
                        findClean.ClearFormatting();
                        findClean.Text = $"{{26_{i}}}";
                        findClean.Replacement.ClearFormatting();
                        findClean.Replacement.Text = "";
                        findClean.Execute(Replace: 2);
                        continue;
                    }

                    try
                    {
                        // 1. Chuẩn bị file Excel checklist
                        dynamic? wb = null;
                        dynamic? sheet = null;
                        dynamic? range = null;
                        string tempXl = Path.Combine(Path.GetTempPath(), $"Checklist_{Guid.NewGuid()}.xlsx");
                        FillChecklistData(xlPath!, tempXl);

                        int chStt = int.TryParse(calculatedStringsMap[i].SttChecklist, out int sc) ? sc : 0;
                        var globalReplacements = GetGlobalReplacements(doc, info, globalConfig, item, chStt);
                        var itemReplacements = GetItemReplacements(item, calculatedStringsMap[i], [.. globalConfig.GlobalStandards], doc, info);
                        foreach (var kvp in itemReplacements) globalReplacements[kvp.Key] = kvp.Value;

                        // 2. Mở Excel, fill data, copy vùng in
                        if (excelApp == null) continue;
                        OfficeInteropSession.KeepExcelHidden(excelApp);
                        wb = excelApp!.Workbooks.Open(tempXl, UpdateLinks: 0, ReadOnly: false, AddToMru: false);
                        // Keep the workbook window available to OLE; the application stays hidden.
                        OfficeInteropSession.KeepExcelHidden(excelApp);
                        sheet = wb.Worksheets[1];
                        FillChecklistViaInterop(sheet, globalReplacements);
                        OfficeInteropSession.KeepExcelHidden(excelApp);
                        
                        string printArea = "";
                        try { printArea = sheet.PageSetup.PrintArea; } catch {}
                        range = !string.IsNullOrEmpty(printArea) ? sheet.Range[printArea] : sheet.UsedRange;
                        try
                        {
                            // Preserve the template's own Unicode-capable fonts. Overriding the
                            // whole range here can corrupt Vietnamese text when Excel is pasted as OLE.
                            excelApp.ErrorCheckingOptions.BackgroundChecking = false;
                        }
                        catch { }
                        range.Copy();
                        OfficeInteropSession.KeepExcelHidden(excelApp);

                        // 3. Tìm placeholder {26_i} và chèn OLE
                        dynamic findObj = wordDoc.Content.Find;
                        findObj.ClearFormatting();
                        findObj.Text = $"{{26_{i}}}";
                        if (findObj.Execute())
                        {
                            dynamic foundRange = findObj.Parent;
                            // Xóa text placeholder trước, sau đó paste OLE vào vị trí đó
                            foundRange.Text = ""; // Xóa text {26_i}
                            foundRange.Collapse(1); // wdCollapseStart = 1, đặt cursor vào vị trí vừa xóa
                            try
                            {
                                foundRange.PasteSpecial(Link: false, DataType: 0, Placement: 0, DisplayAsIcon: false);
                            }
                            catch
                            {
                                foundRange.Paste();
                            }
                        }
                        OfficeInteropSession.KeepExcelHidden(excelApp);

                        // Dọn sạch placeholder còn sót (nếu có)
                        dynamic cleanFind = wordDoc.Content.Find;
                        cleanFind.ClearFormatting();
                        cleanFind.Text = $"{{26_{i}}}";
                        cleanFind.Replacement.ClearFormatting();
                        cleanFind.Replacement.Text = "";
                        cleanFind.Execute(Replace: 2);

                        try { excelApp.CutCopyMode = false; } catch { }
                        OfficeInteropSession.KeepExcelHidden(excelApp);
                        try { wb.Close(false); } catch { }
                        OfficeInteropSession.ReleaseComObject(range);
                        OfficeInteropSession.ReleaseComObject(sheet);
                        OfficeInteropSession.ReleaseComObject(wb);
                        try { File.Delete(tempXl); } catch {}
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Lỗi khi chèn OLE cho dòng {i + 1} (Loại NT: {item.LoaiNt}): {ex.Message}", ex);
                    }
                }

                progress?.Report(new CvxdExportProgress("Đang thay placeholder còn sót...", 94));

                // Second Pass: Dùng Interop Find/Replace để sửa các placeholder bị OpenXML bỏ sót
                // (Do Word chia text ra nhiều Run, OpenXML không tìm được chuỗi hoàn chỉnh)

                // 1. Global replacements (Dùng chung cho cả file)
                var globalReps = GetGlobalReplacements(doc, info, globalConfig);
                foreach (var kvp in globalReps)
                {
                    try
                    {
                        dynamic gFind = wordDoc.Content.Find;
                        gFind.ClearFormatting();
                        gFind.Text = kvp.Key;

                        string replaceVal = kvp.Value ?? "";
                        if (!WordContentContains(wordDoc, kvp.Key)) continue;

                        if (replaceVal.Length < 255 && !replaceVal.Contains('\n') && !replaceVal.Contains('\r'))
                        {
                            gFind.Replacement.ClearFormatting();
                            gFind.Replacement.Text = replaceVal;
                            gFind.Execute(Replace: 2, MatchCase: false); // wdReplaceAll
                        }
                        else
                        {
                            // Nếu chuỗi dài > 255 hoặc có xuống dòng, dùng Selection/Range loop
                            while (gFind.Execute())
                            {
                                dynamic foundRange = gFind.Parent;
                                foundRange.Text = replaceVal;
                                // Quan trọng: Co cụm range về điểm cuối để tránh lặp vô tận nếu replaceVal chứa kvp.Key
                                try { foundRange.Collapse(0); } catch { } // wdCollapseEnd = 0
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogExportWarning($"Failed to replace global placeholder {kvp.Key}", ex);
                    }
                }

                // 2b. Item replacements - đưa lại theo logic backup để xử lý các placeholder còn sót
                // như {NgayNT}, {LoaiBB}, {28} khi OpenXML không bắt được do Word split run.
                var calculatedStringsMap2 = CalculateSttBbForExport(allItems, items, info.KyHieuNhaThau);
                for (int i = 0; i < items.Count; i++)
                {
                    var itemReps = GetItemReplacements(items[i], calculatedStringsMap2[i], [.. globalConfig.GlobalStandards], doc, info);
                    var fallbackReps = GetItemScopedFallbackReplacements(itemReps);
                    try { ReplaceItemScopedFallbacks(wordDoc, i, fallbackReps); }
                    catch (Exception ex)
                    {
                        LogExportWarning($"Failed to replace scoped placeholders for item {i + 1}", ex);
                    }
                }
                RemoveItemMarkers(wordDoc, items.Count);

                // KHẮC PHỤC LỖI FONT: Reset font cho toàn bộ document sau khi chèn OLE
                // Khi Word chèn OLE vào document có bảng phức tạp (TA, TK), nó có thể thay đổi
                // default style và làm lỗi font của các biên bản khác trong cùng file.
                if (items.Count <= 20)
                {
                    progress?.Report(new CvxdExportProgress("Đang chuẩn hóa font Word...", 96));
                    try
                    {
                        // Duyệt qua tất cả paragraphs và reset font về Times New Roman nếu bị thay đổi
                        foreach (dynamic para in wordDoc.Paragraphs)
                        {
                            try
                            {
                                dynamic paraRange = para.Range;
                                // Chỉ reset nếu font không phải Times New Roman (tránh thay đổi không cần thiết)
                                string currentFont = "";
                                try { currentFont = paraRange.Font.Name; } catch { }
                                
                                if (!string.IsNullOrEmpty(currentFont) && 
                                    currentFont != "Times New Roman" && 
                                    currentFont != "+Body" &&
                                    currentFont != "+Headings")
                                {
                                    paraRange.Font.Name = "Times New Roman";
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                progress?.Report(new CvxdExportProgress("Đang lưu file Word...", 98));
                wordDoc.Save();
                wordDoc.Close();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                interopError = ex;
                tcs.SetResult(false);
            }
            finally
            {
                if (excelApp != null)
                {
                    OfficeInteropSession.QuitAndRelease(excelApp, restoreExcelInteraction: true);
                }

                if (wordApp != null)
                {
                    OfficeInteropSession.QuitAndRelease(wordApp);
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Priority = ThreadPriority.BelowNormal; // Đặt độ ưu tiên thấp để không làm lag hệ thống
        staThread.Start();
        
        await tcs.Task; // Đợi một cách bất đồng bộ

        if (interopError != null)
        {
            throw new Exception($"Lỗi khi chèn OLE: {interopError.Message}", interopError);
        }
    }

    private string GetChecklistFolder(CvxdGlobalConfig globalConfig, string? profile = null)
    {
        return TemplateProfileResolver.GetChecklistRoot(globalConfig, profile);
    }

    private static bool NeedsChecklist(CvxdItem item)
    {
        return item.LoaiNt == "CL" || (item.LoaiNt != "CL" && item.SoChecklist == "1");
    }

    private static string? FindChecklistExcel(CvxdItem item, string checklistFolder)
    {
        if (!Directory.Exists(checklistFolder)) return null;
        
        if (!string.IsNullOrEmpty(item.ChecklistId))
        {
            var match = Directory.GetFiles(checklistFolder, "*.xlsx")
                                .FirstOrDefault(f => Path.GetFileName(f).StartsWith(item.ChecklistId));
            if (match != null) return match;
        }
        if (!string.IsNullOrEmpty(item.LoaiBb))
        {
            var match = Directory.GetFiles(checklistFolder, "*.xlsx")
                                .FirstOrDefault(f => Path.GetFileName(f).Contains(item.LoaiBb, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return null;
    }

    private static void FillChecklistData(string sourcePath, string targetPath)
    {
        // Chỉ copy file, thay thế placeholder sẽ dùng Interop trong ProcessOleWithInterop
        File.Copy(sourcePath, targetPath, true);
    }

    private static void FillChecklistViaInterop(dynamic sheet, Dictionary<string, string> replacements)
    {
        foreach (var kvp in replacements)
        {
            try { sheet.Cells.Replace(kvp.Key, kvp.Value ?? ""); }
            catch (Exception ex)
            {
                LogExportWarning($"Failed to replace checklist placeholder {kvp.Key}", ex);
            }
        }
    }
    private static Dictionary<string, string> GetGlobalReplacements(DocumentManagement.Models.Document doc, CvxdInfo info, CvxdGlobalConfig globalConfig, CvxdItem? item = null, int actualStt = 0)
    {
        var result = PlaceholderRegistry.BuildCvxd(doc, info, globalConfig, item);

        // 1. Thêm các placeholder TOÀN CỤC (từ Config)
        if (globalConfig.GlobalPlaceholders != null)
        {
            foreach (var gp in globalConfig.GlobalPlaceholders)
            {
                if (PlaceholderRegistry.IsSystemPlaceholder(gp.Key)) continue;
                if (!string.IsNullOrEmpty(gp.Key))
                {
                    result[gp.Key] = CvxdPlaceholderResolver.Resolve(gp.Value ?? "", doc, info, item);
                }
            }
        }

        // 2. Thêm các placeholder MẶC ĐỊNH (Cứng) - Đè lên Global Config nếu trùng
        var defaultReps = new Dictionary<string, string>
        {
            { "{TenDuAn}", info.TenDuAn ?? "" },
            { "{TenCongTrinh}", info.TenCongTrinh ?? "" },
            { "{HangMuc}", info.TenHangMuc ?? "" },
            { "{DiaDiem}", info.DiaDiem ?? "" },
            { "{Diadiem}", info.DiaDiem ?? "" },
            { "{SoHD}", doc.ContractNumber ?? "" },
            { "{NgayKy}", !string.IsNullOrEmpty(info.NgayKyHD) ? info.NgayKyHD : doc.SignDate.ToString("dd/MM/yyyy") },
            { "{ngayky}", !string.IsNullOrEmpty(info.NgayKyHD) ? info.NgayKyHD : doc.SignDate.ToString("dd/MM/yyyy") },
            { "{NGAYKY}", !string.IsNullOrEmpty(info.NgayKyHD) ? info.NgayKyHD : doc.SignDate.ToString("dd/MM/yyyy") },
            { "{TenNTC}", info.TenChuDauTu ?? "" },
            { "{DaiDienNTC}", info.DaiDienChuDauTu ?? "" },
            { "{ChucVuNTC}", info.ChucVuCDT ?? "" },
            { "{ChucvuNTC}", info.ChucVuCDT ?? "" },
            { "{TenNTP}", info.TenNhaThau ?? "" },
            { "{DaiDienNTP}", info.DaiDienNhaThau ?? "" },
            { "{ChucVuNTP}", info.ChucVuNT ?? "" },
            { "{ChucvuNTP}", info.ChucVuNT ?? "" },
            { "{TenTVGS}", info.TenTuVanGiamSat ?? "" },
            { "{DaiDienTVGS}", info.DaiDienTVGS ?? "" },
            { "{ChucVuTVGS}", info.ChucVuTVGS ?? "" },
            { "{ChucvuTVGS}", info.ChucVuTVGS ?? "" },
            { "{TenNhaThauPhu}", info.TenNhaThauPhu ?? "" },
            { "{DaiDienNTP_Phu}", info.DaiDienNhaThauPhu ?? "" },
            { "{CanCuThiCong}", info.CanCuThiCong ?? "" },
            { "{SoHĐDT}", info.SoHopDongDauTu ?? "" },
            { "{DuAn}", info.TenDuAn ?? "" },
            { "{CongTrinh}", info.TenCongTrinh ?? "" },
            { "{NhaThau}", info.TenNhaThau ?? "" }
        };
        foreach (var kvp in defaultReps) result[kvp.Key] = kvp.Value;

        // 3. Thêm các placeholder THEO HỢP ĐỒNG (Local) - Đè lên trên nếu trùng key
        // Placeholder khong co dinh da duoc PlaceholderRegistry.BuildCvxd ap dung theo context dong.
        // Khong resolve lai cp.Value o day, vi cp.Value la ten cot mapping, khong phai gia tri xuat ra.

        // 4. Xử lý STT đặc biệt: Nếu có STT đếm thực tế (dùng cho Checklist), phải được ưu tiên cuối cùng
        if (actualStt > 0)
        {
            result["{STT}"] = actualStt.ToString();
        }

        return result;
    }

    public class CvxdItemCalculatedStrings
    {
        public string Ycnt { get; set; } = string.Empty;
        public string Nt { get; set; } = string.Empty;
        public string SttBb { get; set; } = string.Empty;
        public string SttChecklist { get; set; } = "0";
        public string ChecklistReferences { get; set; } = string.Empty;
    }

    /// <summary>Tính STT_BB theo từng loại NT, trả về các chuỗi YCNT, NT cho mỗi item</summary>
    private static Dictionary<int, CvxdItemCalculatedStrings> CalculateSttBb(List<CvxdItem> items, string? contractorCode)
    {
        var counters = new Dictionary<string, int>(); // LoaiNT → counter
        int globalChCounter = 0; 
        var result = new Dictionary<int, CvxdItemCalculatedStrings>();

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            string loaiNt = (item.LoaiNt ?? "CVXD").Trim();

            // Chỉ đếm STT biên bản cho các dòng KHÔNG phải CL
            if (loaiNt != "CL")
            {
                if (!counters.ContainsKey(loaiNt)) counters[loaiNt] = 0;
                counters[loaiNt]++;
            }

            // Đếm STT Checklist khi:
            // 1. Là dòng CL
            // 2. Hoặc là dòng chính nhưng chỉ có 1 checklist (chèn trực tiếp)
            bool shouldInsertChecklist = (loaiNt == "CL") || (loaiNt != "CL" && item.SoChecklist == "1");
            if (shouldInsertChecklist) globalChCounter++;

            string sttBb = (loaiNt != "CL") ? counters[loaiNt].ToString("D2") : "";
            result[i] = new CvxdItemCalculatedStrings
            {
                SttBb = sttBb,
                Ycnt = !string.IsNullOrEmpty(sttBb) ? AppendContractorCode($"{sttBb}/YCNT/{loaiNt}", contractorCode) : "",
                Nt = !string.IsNullOrEmpty(sttBb) 
                    ? AppendContractorCode((loaiNt == "TA" || loaiNt == "TK") ? $"{sttBb}/KT/{loaiNt}" : $"{sttBb}/NT/{loaiNt}", contractorCode) 
                    : "",
                SttChecklist = shouldInsertChecklist ? globalChCounter.ToString() : "0"
            };
        }

        // A parent acceptance row can declare several checklists. Those checklists
        // are represented by consecutive CL rows immediately below it. Keep their
        // generated numbers on the parent so {28} lists every related checklist.
        for (var parentIndex = 0; parentIndex < items.Count; parentIndex++)
        {
            var parent = items[parentIndex];
            if (string.Equals(parent.LoaiNt?.Trim(), "CL", StringComparison.OrdinalIgnoreCase)
                || !int.TryParse(parent.SoChecklist, out var checklistCount)
                || checklistCount <= 1)
            {
                continue;
            }

            var references = new List<string>();
            for (var childIndex = parentIndex + 1;
                 childIndex < items.Count && references.Count < checklistCount;
                 childIndex++)
            {
                var child = items[childIndex];
                if (!string.Equals(child.LoaiNt?.Trim(), "CL", StringComparison.OrdinalIgnoreCase))
                {
                    // A different main acceptance row starts a new group.
                    break;
                }

                var childChecklistNumber = result[childIndex].SttChecklist;
                if (childChecklistNumber != "0")
                {
                    references.Add(AppendContractorCode($"{childChecklistNumber}/KT/ĐTNT", contractorCode));
                }
            }

            result[parentIndex].ChecklistReferences = string.Join("; ", references);
        }

        return result;
    }

    private static string AppendContractorCode(string value, string? contractorCode)
    {
        return string.IsNullOrWhiteSpace(contractorCode)
            ? value
            : $"{value}/{contractorCode.Trim()}";
    }

    /// <summary>
    /// Projects the full-table sequence onto only the items being exported. This preserves the
    /// original YCNT/NT number when the user exports a subset of the CVXD table.
    /// </summary>
    private static Dictionary<int, CvxdItemCalculatedStrings> CalculateSttBbForExport(
        List<CvxdItem> allItems,
        List<CvxdItem> exportItems,
        string? contractorCode)
    {
        var fullSequence = CalculateSttBb(allItems, contractorCode);
        var result = new Dictionary<int, CvxdItemCalculatedStrings>();

        for (int exportIndex = 0; exportIndex < exportItems.Count; exportIndex++)
        {
            var sourceIndex = allItems.FindIndex(item => ReferenceEquals(item, exportItems[exportIndex]));
            if (sourceIndex >= 0 && fullSequence.TryGetValue(sourceIndex, out var calculated))
            {
                result[exportIndex] = calculated;
                continue;
            }

            // Defensive fallback for callers that pass detached item instances.
            result[exportIndex] = CalculateSttBb(exportItems, contractorCode)[exportIndex];
        }

        return result;
    }

    private static Dictionary<string, string> GetItemReplacements(CvxdItem item, CvxdItemCalculatedStrings calculated, List<CvxdStandard>? standards, DocumentManagement.Models.Document doc, CvxdInfo info)
    {
        // Logic cho mục {LoaiBB}
        string displayLoaiBB = "";
        if (IsCvxdType(item.LoaiNt)) displayLoaiBB = "NGHIỆM THU CÔNG VIỆC XÂY DỰNG";
        else if (IsLdttbType(item.LoaiNt)) displayLoaiBB = "NGHIỆM THU LẮP ĐẶT TĨNH THIẾT BỊ";

        // Tra cứu tiêu chuẩn (TCNT)
        string tcntValue = item.Tcnt ?? "";
        if (standards != null && !string.IsNullOrEmpty(tcntValue))
        {
            var matched = standards.FirstOrDefault(s => s.Key != null && s.Key.Equals(tcntValue, StringComparison.OrdinalIgnoreCase));
            if (matched != null && !string.IsNullOrEmpty(matched.Value))
            {
                tcntValue = matched.Value;
            }
        }

        // Logic cho mục {19} theo Loại NT
        string value19 = "";
        if (IsCvxdType(item.LoaiNt) || item.LoaiNt == "ĐĐKT" || item.LoaiNt == "LDKT")
        {
            value19 = "- Kế hoạch kiểm tra – thí nghiệm (ITP) đã được phê duyệt;";
        }
        else if (IsLdttbType(item.LoaiNt))
        {
            value19 = "-   Biên bản nghiệm thu vật liệu, thiết bị, sản phẩm chế tạo sẵn trước khi sử dụng;";
        }

        // Logic cho mục {28} theo Loại NT
        string value28 = "";
        bool hasChecklist = !string.IsNullOrEmpty(item.SoChecklist) && item.SoChecklist != "0";
        // A multi-checklist parent uses the generated numbers of its consecutive CL rows.
        // A normal one-checklist row still uses its own generated checklist number.
        string soBB = !string.IsNullOrWhiteSpace(calculated.ChecklistReferences)
            ? calculated.ChecklistReferences
            : calculated.SttChecklist != "0"
                ? AppendContractorCode($"{calculated.SttChecklist}/KT/ĐTNT", info.KyHieuNhaThau)
                : "";
        string soCL = hasChecklist ? soBB : "";
        
        if (IsCvxdType(item.LoaiNt) || item.LoaiNt == "ĐĐKT" || item.LoaiNt == "LDKT")
        {
            value28 = hasChecklist
                ? $"-   Danh mục kiểm tra (check list) số: {soBB};"
                : "-   Danh mục kiểm tra (check list) số:";
        }
        else if (IsLdttbType(item.LoaiNt))
        {
            value28 = hasChecklist
                ? $"-   Các phiếu kiểm tra đối tượng nghiệm thu: {soBB};"
                : "-   Các phiếu kiểm tra đối tượng nghiệm thu:";
        }

        var result = new Dictionary<string, string>
        {
            // Các mã tính toán đặc thù
            { "{19}", value19 },
            { "{28}", value28 },
            { "{SoCL}", soCL },
            { "{YCNT}", calculated.Ycnt },
            { "{NT}", calculated.Nt },
            { "{TC}", tcntValue },
            { "{STT_BB}", calculated.SttBb },
            { "{STT_checklist}", calculated.SttChecklist != "0" ? calculated.SttChecklist : "" },
            { "{LoaiBB}", displayLoaiBB },
            { "{LoaiBb}", displayLoaiBB },

            // Bổ sung các mã truyền thống cho Checklist và Word
            { "{TenDuAn}", info.TenDuAn ?? "" },
            { "{TenCongTrinh}", info.TenCongTrinh ?? "" },
            { "{HangMuc}", info.TenHangMuc ?? "" },
            { "{DiaDiem}", info.DiaDiem ?? "" },
            { "{Diadiem}", info.DiaDiem ?? "" },
            { "{SoHD}", doc.ContractNumber ?? "" },
            { "{NgayKy}", !string.IsNullOrEmpty(info.NgayKyHD) ? info.NgayKyHD : doc.SignDate.ToString("dd/MM/yyyy") },
            { "{ngayky}", !string.IsNullOrEmpty(info.NgayKyHD) ? info.NgayKyHD : doc.SignDate.ToString("dd/MM/yyyy") },
            { "{NGAYKY}", !string.IsNullOrEmpty(info.NgayKyHD) ? info.NgayKyHD : doc.SignDate.ToString("dd/MM/yyyy") },
            { "{TenNTC}", info.TenChuDauTu ?? "" },
            { "{DaiDienNTC}", info.DaiDienChuDauTu ?? "" },
            { "{TenNTP}", info.TenNhaThau ?? "" },
            { "{DaiDienNTP}", info.DaiDienNhaThau ?? "" },
            { "{TenTVGS}", info.TenTuVanGiamSat ?? "" },
            { "{DaiDienTVGS}", info.DaiDienTVGS ?? "" },
            
            // Item fields
            { "{STT}", (calculated.SttChecklist != "0") ? calculated.SttChecklist : (item.Stt ?? "") },
            { "{NoiDung}", item.TenNt?.ToUpper() ?? "" },
            { "{NgayNT}", item.NgayNt ?? "" },
            { "{Ngay_NT}", item.NgayNt ?? "" },
            { "{BatDau}", item.BatDau ?? "" },
            { "{KetThuc}", item.KetThuc ?? "" },
            { "{SoChecklist}", item.SoChecklist ?? "" },
            { "{DuAn}", info.TenDuAn ?? "" },
            { "{CongTrinh}", info.TenCongTrinh ?? "" },
            { "{NhaThau}", info.TenNhaThau ?? "" },
            { "{TenCauKien}", item.NoiDung ?? "" },
            { "{PDVT}", item.SoPdvt ?? "" }
        };

        // 3. Tính toán ngày cho Word template
        // {15},{16},{17} = ngày/tháng/năm Nghiệm thu
        // {1},{2},{3} = ngày/tháng/năm gửi Công văn (trước ngày NT 1-2 ngày)
        if (DateTime.TryParseExact(item.NgayNt, ["dd/MM/yyyy", "d/M/yyyy", "dd/M/yyyy", "d/MM/yyyy"], 
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime ngayNT))
        {
            // Ngày Nghiệm thu
            result["{15}"] = ngayNT.Day.ToString("D2");
            result["{16}"] = ngayNT.Month.ToString("D2");
            result["{17}"] = ngayNT.Year.ToString();

            // Ngày gửi Công văn: NT - 1 ngày. Nếu là Thứ 2 thì lùi 2 ngày (về Thứ 7)
            int daysToSubtract = (ngayNT.DayOfWeek == DayOfWeek.Monday) ? 2 : 1;
            DateTime ngayCongVan = ngayNT.AddDays(-daysToSubtract);
            result["{1}"] = ngayCongVan.Day.ToString("D2");
            result["{2}"] = ngayCongVan.Month.ToString("D2");
            result["{3}"] = ngayCongVan.Year.ToString();
        }
        else
        {
            // Fallback nếu không parse được ngày
            result["{1}"] = ""; result["{2}"] = ""; result["{3}"] = "";
            result["{15}"] = ""; result["{16}"] = ""; result["{17}"] = "";
        }

        return result;
    }

    private static Dictionary<string, string> GetCommonItemFallbackReplacements(
        List<CvxdItem> items,
        Dictionary<int, CvxdItemCalculatedStrings> calculatedMap,
        List<CvxdStandard>? standards,
        DocumentManagement.Models.Document doc,
        CvxdInfo info)
    {
        var keys = new[] { "{LoaiBB}", "{LoaiBb}", "{28}", "{SoCL}" };
        var valuesByKey = keys.ToDictionary(k => k, _ => new List<string>());

        for (int i = 0; i < items.Count; i++)
        {
            if (!calculatedMap.TryGetValue(i, out var calculated)) continue;
            var replacements = GetItemReplacements(items[i], calculated, standards, doc, info);
            foreach (var key in keys)
            {
                if (replacements.TryGetValue(key, out var value))
                    valuesByKey[key].Add(value ?? string.Empty);
            }
        }

        var result = new Dictionary<string, string>();
        foreach (var (key, values) in valuesByKey)
        {
            if (values.Count == 0) continue;
            var first = values[0];
            if (values.All(v => string.Equals(v, first, StringComparison.Ordinal)))
            {
                result[key] = first;
            }
        }

        return result;
    }

    private static Dictionary<string, string> GetItemScopedFallbackReplacements(Dictionary<string, string> itemReplacements)
    {
        string[] keys =
        [
            "{28}",
            "{SoCL}",
            "{STT_checklist}",
            "{LoaiBB}",
            "{LoaiBb}",
            "{NgayNT}",
            "{Ngay_NT}",
            "{BatDau}",
            "{KetThuc}",
            "{STT}",
            "{STT_BB}",
            "{YCNT}",
            "{NT}"
        ];

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            if (itemReplacements.TryGetValue(key, out var value))
            {
                result[key] = value ?? string.Empty;
            }
        }

        return result;
    }

    private static void ReplaceItemScopedFallbacks(dynamic wordDoc, int itemIndex, Dictionary<string, string> replacements)
    {
        if (replacements.Count == 0) return;

        var startMarker = GetItemStartMarker(itemIndex);
        var endMarker = GetItemEndMarker(itemIndex);
        if (!TryGetRangeBetweenMarkers(wordDoc, startMarker, endMarker, out dynamic? itemRange))
            return;

        foreach (var kvp in replacements)
        {
            if (!RangeContentContains(itemRange, kvp.Key)) continue;
            ReplaceWithWordFind(itemRange, kvp.Key, kvp.Value ?? string.Empty);
        }
    }

    private static bool TryGetRangeBetweenMarkers(dynamic wordDoc, string startMarker, string endMarker, out dynamic? itemRange)
    {
        itemRange = null;

        try
        {
            dynamic content = wordDoc.Content;
            dynamic startSearch = content.Duplicate;
            dynamic startFind = startSearch.Find;
            startFind.ClearFormatting();
            startFind.Text = startMarker;
            startFind.Wrap = 0;
            if (!startFind.Execute()) return false;

            int rangeStart = startSearch.End;
            dynamic endSearch = wordDoc.Range(rangeStart, content.End);
            dynamic endFind = endSearch.Find;
            endFind.ClearFormatting();
            endFind.Text = endMarker;
            endFind.Wrap = 0;
            if (!endFind.Execute()) return false;

            int rangeEnd = endSearch.Start;
            if (rangeEnd < rangeStart) return false;

            itemRange = wordDoc.Range(rangeStart, rangeEnd);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool RangeContentContains(dynamic range, string find)
    {
        if (string.IsNullOrWhiteSpace(find)) return false;

        try
        {
            string text = range.Text;
            return text.Contains(find, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static void RemoveItemMarkers(dynamic wordDoc, int itemCount)
    {
        for (int i = 0; i < itemCount; i++)
        {
            try { ReplaceWithWordFind(wordDoc, GetItemStartMarker(i), string.Empty); } catch { }
            try { ReplaceWithWordFind(wordDoc, GetItemEndMarker(i), string.Empty); } catch { }
        }
    }

    private static void ReplaceWithWordFind(dynamic target, string find, string replaceVal)
    {
        if (string.IsNullOrWhiteSpace(find)) return;

        dynamic searchRange;
        try { searchRange = target.Content; }
        catch { searchRange = target; }

        dynamic findObj = searchRange.Find;
        findObj.ClearFormatting();
        findObj.Text = find;
        try { findObj.Wrap = 0; } catch { }

        if (replaceVal.Length < 255 && !replaceVal.Contains('\n') && !replaceVal.Contains('\r'))
        {
            findObj.Replacement.ClearFormatting();
            findObj.Replacement.Text = replaceVal;
            findObj.Execute(Replace: 2);
            return;
        }

        while (findObj.Execute())
        {
            dynamic foundRange = findObj.Parent;
            foundRange.Text = replaceVal;
            try { foundRange.Collapse(0); } catch { }
        }
    }

    private static bool WordContentContains(dynamic wordDoc, string find)
    {
        if (string.IsNullOrWhiteSpace(find)) return false;

        try
        {
            string text = wordDoc.Content.Text;
            return text.Contains(find, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static bool IsCvxdType(string? loaiNt)
    {
        return string.Equals(loaiNt?.Trim(), "CVXD", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLdttbType(string? loaiNt)
    {
        var value = loaiNt?.Trim();
        return string.Equals(value, "LĐTTB", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "LDTTB", StringComparison.OrdinalIgnoreCase);
    }
}
