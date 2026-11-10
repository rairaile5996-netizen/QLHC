using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentManagement.Models;
using DocumentManagement.Utilities;

namespace DocumentManagement.Services;

public partial class VtdvExportService
{
    private readonly string _basePath;
    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "dd/M/yyyy", "d/MM/yyyy"];

    private sealed record VtdvChecklistWorkItem(VtdvSession Session, string Marker);

    [System.Text.RegularExpressions.GeneratedRegex(@"\d+")]
    private static partial System.Text.RegularExpressions.Regex DigitRegex();

    public VtdvExportService()
    {
        _basePath = AppDomain.CurrentDomain.BaseDirectory;
    }

    public CvxdExportPreflightResult ValidateExport(CvxdGlobalConfig globalConfig, List<VtdvSession> sessions, string? templatePath, string? profile = null)
    {
        var result = new CvxdExportPreflightResult
        {
            SelectedCount = sessions.Count,
            ChecklistCount = sessions.Count
        };

        if (sessions.Count == 0)
        {
            result.Errors.Add("Vui lòng chọn ít nhất một biên bản VTĐV để xuất.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
        {
            result.Errors.Add($"Thiếu template Word VTĐV: {templatePath}");
        }
        else if (ResolveChecklistTemplate(globalConfig, templatePath, profile) is null)
        {
            result.Errors.Add("Thiếu checklist_template.xlsx cho VTĐV. Kiểm tra thư mục Checklist dùng chung hoặc thư mục VTĐV.");
        }

        foreach (var session in sessions)
        {
            var label = string.IsNullOrWhiteSpace(session.SoBienBan) ? "(chưa có số biên bản)" : session.SoBienBan;

            if (string.IsNullOrWhiteSpace(session.SoBienBan))
                result.Errors.Add("Có biên bản VTĐV chưa có số biên bản.");

            if (!DateTime.TryParseExact(session.NgayNghiệmThu, DateFormats,
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                result.Errors.Add($"Biên bản {label}: ngày nghiệm thu không hợp lệ.");

            if (string.IsNullOrWhiteSpace(session.DoiTuongNghiệmThu))
                result.Errors.Add($"Biên bản {label}: thiếu đối tượng nghiệm thu.");

            if (session.Items.Count == 0)
            {
                result.Errors.Add($"Biên bản {label}: chưa có vật tư.");
                continue;
            }

            foreach (var item in session.Items.Where(IsBlankMaterialItem))
            {
                result.Warnings.Add($"Biên bản {label}: có dòng vật tư trống STT {item.Stt}.");
            }

            foreach (var item in session.Items.Where(i => !IsBlankMaterialItem(i)))
            {
                var itemLabel = string.IsNullOrWhiteSpace(item.Stt) ? "?" : item.Stt;
                if (string.IsNullOrWhiteSpace(item.DoiTuong) ||
                    string.IsNullOrWhiteSpace(item.DonVi) ||
                    string.IsNullOrWhiteSpace(item.SoLuong))
                {
                    result.Errors.Add($"Biên bản {label}: dòng vật tư {itemLabel} thiếu tên vật tư, đơn vị hoặc số lượng.");
                }
            }
        }

        return result;
    }

    public async Task<string> ExportToWordAsync(DocumentManagement.Models.Document doc, VtdvSession session, DocumentManagement.Models.CvxdInfo projectInfo, CvxdGlobalConfig globalConfig, string customTemplatePath = "", IProgress<CvxdExportProgress>? progress = null)
    {
        try
        {
            string templatePath = !string.IsNullOrEmpty(customTemplatePath)
                ? customTemplatePath
                : Path.Combine(_basePath, "Templates", "WordTemplates", "VTDV_Template.docx");

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Không tìm thấy file mẫu Word tại: {templatePath}");
            }

            string outputDir = Path.Combine(doc.FolderPath ?? _basePath, "3.HSCL", "VTĐV");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            string fileName = $"VTDV_{SanitizeFileName(session.SoBienBan)}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            string outputPath = Path.Combine(outputDir, fileName);
            var checklistTemplatePath = ResolveChecklistTemplate(globalConfig, templatePath, projectInfo.TemplateProfile)
                ?? throw new FileNotFoundException("Không tìm thấy checklist_template.xlsx cho VTĐV.");

            progress?.Report(new CvxdExportProgress("Đang copy template Word VTĐV...", 10));
            File.Copy(templatePath, outputPath, true);

            progress?.Report(new CvxdExportProgress($"Đang xử lý biên bản {session.SoBienBan}...", 35));
            string marker = string.Empty;
            await Task.Run(() =>
            {
                using var wordDoc = WordprocessingDocument.Open(outputPath, true);
                marker = ProcessSession(wordDoc.MainDocumentPart!, session, projectInfo, globalConfig, doc);
            });

            // LuÃ´n xá»­ lÃ½ chÃ¨n OLE vÃ¬ user muá»‘n tá»± Ä‘á»™ng
            await ProcessOleWithInterop(outputPath, doc, projectInfo, globalConfig, [new VtdvChecklistWorkItem(session, marker)], checklistTemplatePath, progress);

            ExportedFileRegistry.Record(doc.FolderPath, outputPath, "VTĐV");
            progress?.Report(new CvxdExportProgress("Đang mở file Word kết quả...", 95));
            Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
            return outputPath;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Lỗi xuất file Word VTĐV: {ex.Message}", ex);
        }
    }

    public async Task<string> ExportAllToWordAsync(DocumentManagement.Models.Document doc, List<VtdvSession> sessions, DocumentManagement.Models.CvxdInfo projectInfo, CvxdGlobalConfig globalConfig, string templatePath, IProgress<CvxdExportProgress>? progress = null)
    {
        try
        {
            if (!File.Exists(templatePath)) throw new FileNotFoundException($"Không tìm thấy file mẫu Word tại: {templatePath}");

            string outputDir = Path.Combine(doc.FolderPath ?? _basePath, "3.HSCL", "VTĐV");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            string fileName = $"VTDV_TAT_CA_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            string outputPath = Path.Combine(outputDir, fileName);
            var checklistTemplatePath = ResolveChecklistTemplate(globalConfig, templatePath, projectInfo.TemplateProfile)
                ?? throw new FileNotFoundException("Không tìm thấy checklist_template.xlsx cho VTĐV.");
            var checklistItems = new List<VtdvChecklistWorkItem>();

            progress?.Report(new CvxdExportProgress("Đang copy template Word VTĐV...", 10));
            File.Copy(templatePath, outputPath, true);

            await Task.Run(() =>
            {
                using var mainDoc = WordprocessingDocument.Open(outputPath, true);
                var mainPart = mainDoc.MainDocumentPart!;
                
                if (sessions.Count > 0)
                {
                    // Process first session
                    progress?.Report(new CvxdExportProgress($"Đang xử lý biên bản 1/{sessions.Count}...", 20));
                    checklistItems.Add(new VtdvChecklistWorkItem(
                        sessions[0],
                        ProcessSession(mainPart, sessions[0], projectInfo, globalConfig, doc)));

                    // Process subsequent sessions
                    for (int i = 1; i < sessions.Count; i++)
                    {
                        progress?.Report(new CvxdExportProgress($"Đang xử lý biên bản {i + 1}/{sessions.Count}...", 20 + ((i + 1) * 40.0 / sessions.Count)));
                        mainPart.Document.Body!.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
                        
                        using (var tempDoc = WordprocessingDocument.Open(templatePath, false))
                        {
                            var tempBody = tempDoc.MainDocumentPart!.Document.Body!;
                            foreach (var element in tempBody.Elements())
                            {
                                mainPart.Document.Body.AppendChild(element.CloneNode(true));
                            }
                        }

                        checklistItems.Add(new VtdvChecklistWorkItem(
                            sessions[i],
                            ProcessSession(mainPart, sessions[i], projectInfo, globalConfig, doc)));
                    }
                }
                mainPart.Document.Save();
            });

            // LuÃ´n xá»­ lÃ½ chÃ¨n OLE hÃ ng loáº¡t
            await ProcessOleWithInterop(outputPath, doc, projectInfo, globalConfig, checklistItems, checklistTemplatePath, progress);

            ExportedFileRegistry.Record(doc.FolderPath, outputPath, "VTĐV");
            progress?.Report(new CvxdExportProgress("Đang mở file Word kết quả...", 95));
            Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
            return outputPath;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Lỗi xuất tất cả biên bản VTĐV: {ex.Message}", ex);
        }
    }

    private static string ProcessSession(MainDocumentPart mainPart, VtdvSession session, DocumentManagement.Models.CvxdInfo projectInfo, CvxdGlobalConfig _, DocumentManagement.Models.Document doc)
    {
        var body = mainPart.Document.Body!;
        

        var replacements = PlaceholderRegistry.BuildVtdv(doc, projectInfo, _, session);

        // TÃ­ch há»£p checklist: Náº¿u cÃ³ {CL} thÃ¬ chuyá»ƒn thÃ nh mÃ£ táº¡m thá» i cho Interop
        string clMarker = $"_CL_MARKER_{Guid.NewGuid()}_";
        replacements["{CL}"] = clMarker;

        var tables = body.Descendants<Table>().ToList();
        foreach (var table in tables)
        {
            if (HasPlaceholder(table, "{TenVT}"))
            {
                FillMaterialTable(table, [.. session.Items], projectInfo, session);
            }
        }

        ReplaceTextInBody(mainPart, replacements);

        return clMarker;
    }

    private static async Task ProcessOleWithInterop(string wordPath, DocumentManagement.Models.Document doc, CvxdInfo projectInfo, CvxdGlobalConfig globalConfig, List<VtdvChecklistWorkItem> checklistItems, string checklistTemplatePath, IProgress<CvxdExportProgress>? progress)
    {
        var tcs = new TaskCompletionSource<bool>();
        var staThread = new System.Threading.Thread(() =>
        {
            dynamic? wordApp = null;
            dynamic? excelApp = null;
            try
            {
                wordApp = OfficeInteropSession.CreateWordApplication();
                excelApp = OfficeInteropSession.CreateExcelApplication(disableInteraction: false);
                if (wordApp == null || excelApp == null) throw new Exception("Yêu cầu cài đặt Word và Excel để dùng tính năng Checklist.");

                dynamic safeWordApp = wordApp!;
                dynamic safeExcelApp = excelApp!;

                dynamic wordDoc = safeWordApp.Documents.Open(wordPath);
                safeWordApp.Visible = false;
                safeWordApp.ShowWindowsInTaskbar = false;
                OfficeInteropSession.HideWindow(safeWordApp);

                for (int i = 0; i < checklistItems.Count; i++)
                {
                    var session = checklistItems[i].Session;
                    progress?.Report(new CvxdExportProgress($"Đang chèn checklist biên bản {i + 1}/{checklistItems.Count}...", 65 + ((i + 1) * 25.0 / checklistItems.Count)));
                    string marker = checklistItems[i].Marker;
                    if (string.IsNullOrEmpty(marker) || !marker.StartsWith("_CL_MARKER_")) continue;

                    if (!File.Exists(checklistTemplatePath))
                    {
                        // XÃ³a marker náº¿u khÃ´ng tÃ¬m tháº¥y checklist
                        dynamic f = wordDoc.Content.Find;
                        f.Text = marker;
                        f.Replacement.Text = "";
                        f.Execute(Replace: 2, MatchCase: false);
                        continue;
                    }

                    string tempXl = Path.Combine(Path.GetTempPath(), $"VTDV_CL_{Guid.NewGuid()}.xlsx");
                    File.Copy(checklistTemplatePath, tempXl, true);

                    OfficeInteropSession.HideWindow(safeExcelApp);
                    dynamic wb = safeExcelApp.Workbooks.Open(tempXl, UpdateLinks: 0, ReadOnly: false, AddToMru: false);
                    try { wb.Windows[1].Visible = false; } catch { }
                    OfficeInteropSession.HideWindow(safeExcelApp);
                    dynamic sheet = wb.Worksheets[1];

                    // Fill data vÃ o checklist
                    var reps = GetChecklistReplacements(doc, session, projectInfo, globalConfig);
                    foreach (var kvp in reps)
                    {
                        try { sheet.Cells.Replace(kvp.Key, kvp.Value ?? ""); } catch { }
                    }

                    // Copy vÃ  Paste OLE
                    dynamic range = sheet.UsedRange;
                    range.Copy();
                    OfficeInteropSession.HideWindow(safeExcelApp);

                    dynamic findObj = wordDoc.Content.Find;
                    findObj.Text = marker;
                    if (findObj.Execute(MatchCase: false))
                    {
                        dynamic foundRange = findObj.Parent;
                        foundRange.Text = "";
                        foundRange.Collapse(1);
                        try { foundRange.PasteSpecial(Link: false, DataType: 0, Placement: 0, DisplayAsIcon: false); }
                        catch { foundRange.Paste(); }
                    }

                    // XÃ³a clipboard Ä‘á»ƒ trÃ¡nh hiá»‡n thÃ´ng bÃ¡o khi táº¯t Excel
                    try { safeExcelApp.CutCopyMode = false; } catch { }
                    try { wb.Close(false); } catch { }
                    OfficeInteropSession.ReleaseComObject(range);
                    OfficeInteropSession.ReleaseComObject(sheet);
                    OfficeInteropSession.ReleaseComObject(wb);
                    try { File.Delete(tempXl); } catch { }
                }

                wordDoc.Save();
                wordDoc.Close();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
            finally
            {
                if (excelApp != null)
                {
                    OfficeInteropSession.QuitAndRelease(excelApp);
                }

                if (wordApp != null)
                {
                    OfficeInteropSession.QuitAndRelease(wordApp);
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        });

        staThread.SetApartmentState(System.Threading.ApartmentState.STA);
        staThread.Start();
        await tcs.Task;
    }

    private static Dictionary<string, string> GetChecklistReplacements(DocumentManagement.Models.Document doc, VtdvSession session, CvxdInfo info, CvxdGlobalConfig config)
    {
        
        return PlaceholderRegistry.BuildVtdv(doc, info, config, session);
    }

    private static void FillMaterialTable(Table table, List<VtdvItem> items, CvxdInfo info, VtdvSession session)
    {
        // Giáº£ Ä‘á»‹nh dÃ²ng Ä‘áº§u tiÃªn lÃ  Header, dÃ²ng thá»© hai lÃ  dÃ²ng máº«u chá»©a placeholder
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count < 2) return;

        var templateRow = rows[1];

        for (var rowIndex = 0; rowIndex < items.Count; rowIndex++)
        {
            var item = items[rowIndex];
            if (templateRow.CloneNode(true) is not TableRow newRow) continue;

            var rowReplacements = PlaceholderRegistry.BuildVtdvItem(item, info, session, rowIndex + 1);

            foreach (var cell in newRow.Descendants<TableCell>())
            {
                foreach (var para in cell.Descendants<Paragraph>())
                {
                    // Thay tháº¿ trá»±c tiáº¿p trong Text elements trÆ°á»›c Ä‘á»ƒ giá»¯ Ä‘á»‹nh dáº¡ng
                    var texts = para.Descendants<Text>().ToList();
                    foreach (var t in texts)
                    {
                        foreach (var kvp in rowReplacements)
                        {
                            if (t.Text.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                                t.Text = t.Text.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
                        }
                    }

                    // Xá»­ lÃ½ split runs náº¿u cÃ²n sÃ³t placeholder
                    string paraText = para.InnerText;
                    foreach (var kvp in rowReplacements)
                    {
                        if (paraText.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!para.Descendants<Text>().Any(t => t.Text.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase)))
                            {
                                var firstRun = para.GetFirstChild<Run>();
                                if (firstRun?.RunProperties?.CloneNode(true) is RunProperties rPr)
                                {
                                    para.RemoveAllChildren<Run>();
                                    var newRun = new Run(new Text(paraText.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase)) { Space = SpaceProcessingModeValues.Preserve });
                                    newRun.PrependChild(rPr);
                                    para.AppendChild(newRun);
                                }
                                else
                                {
                                    para.RemoveAllChildren<Run>();
                                    var newRun = new Run(new Text(paraText.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase)) { Space = SpaceProcessingModeValues.Preserve });
                                    para.AppendChild(newRun);
                                }
                                paraText = para.InnerText;
                            }
                        }
                    }
                }
            }

            table.InsertBefore(newRow, templateRow);
        }

        templateRow.Remove();
    }

    private static bool HasPlaceholder(OpenXmlElement element, string placeholder)
    {
        return element.InnerText.Contains(placeholder, StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveChecklistTemplate(CvxdGlobalConfig globalConfig, string wordTemplatePath, string? profile)
    {
        var candidateFolders = new List<string>();

        candidateFolders.Add(TemplateProfileResolver.GetChecklistRoot(globalConfig, profile));

        var wordTemplateFolder = Path.GetDirectoryName(wordTemplatePath);
        if (!string.IsNullOrWhiteSpace(wordTemplateFolder))
            candidateFolders.Add(wordTemplateFolder);

        candidateFolders.Add(TemplateProfileResolver.GetVtdvFolder(TemplateProfileResolver.GetTemplateRoot(globalConfig, profile)));

        candidateFolders.Add(Path.Combine(_basePath, "Templates", "VTĐV"));

        foreach (var folder in candidateFolders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.Combine(folder, "checklist_template.xlsx");
            if (File.Exists(path)) return path;
        }

        return null;
    }

    private static bool IsBlankMaterialItem(VtdvItem item)
    {
        return string.IsNullOrWhiteSpace(item.DoiTuong) &&
               string.IsNullOrWhiteSpace(item.DonVi) &&
               string.IsNullOrWhiteSpace(item.SoLuong) &&
               string.IsNullOrWhiteSpace(item.ThuongHieu) &&
               string.IsNullOrWhiteSpace(item.MaSanPham) &&
               string.IsNullOrWhiteSpace(item.ChatLuong) &&
               string.IsNullOrWhiteSpace(item.GhiChu);
    }

    private static string SanitizeFileName(string? value)
    {
        var name = string.IsNullOrWhiteSpace(value) ? "VTDV" : value;
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '-');
        }

        return name.Trim();
    }

    private static void ReplaceTextInBody(MainDocumentPart mainPart, Dictionary<string, string> replacements)
    {
        // 1. Thay tháº¿ trá»±c tiáº¿p trong cÃ¡c Text elements (Giá»¯ Ä‘á»‹nh dáº¡ng gá»‘c 100%)
        foreach (var text in mainPart.Document.Body!.Descendants<Text>())
        {
            foreach (var kvp in replacements)
            {
                if (text.Text.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    text.Text = text.Text.Replace(kvp.Key, kvp.Value ?? "", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        // 2. Xá»­ lÃ½ cÃ¡c placeholder bá»‹ Word chia nhá» thÃ nh nhiá»u Run (Split Runs)
        foreach (var para in mainPart.Document.Body!.Descendants<Paragraph>())
        {
            string paraText = para.InnerText;
            foreach (var kvp in replacements)
            {
                if (paraText.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // Náº¿u bÆ°á»›c 1 chÆ°a replace Ä‘Æ°á»£c do bá»‹ chia nhá»
                    if (!para.Descendants<Text>().Any(t => t.Text.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        var firstRun = para.GetFirstChild<Run>();
                        string val = kvp.Value ?? "";
                        string newText = paraText.Replace(kvp.Key, val, StringComparison.OrdinalIgnoreCase);
                        if (firstRun?.RunProperties?.CloneNode(true) is RunProperties rPr)
                        {
                            para.RemoveAllChildren<Run>();
                            var newRun = new Run(new Text(newText) { Space = SpaceProcessingModeValues.Preserve });
                            newRun.PrependChild(rPr);
                            para.AppendChild(newRun);
                        }
                        else
                        {
                            para.RemoveAllChildren<Run>();
                            var newRun = new Run(new Text(newText) { Space = SpaceProcessingModeValues.Preserve });
                            para.AppendChild(newRun);
                        }
                        
                        paraText = newText;
                    }
                }
            }
        }
    }
}

