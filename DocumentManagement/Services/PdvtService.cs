using System.Text.Json;
using System.IO;
using ClosedXML.Excel;
using DocumentManagement.Models;
using DocumentFormat.OpenXml.Packaging;
using Spreadsheet = DocumentFormat.OpenXml.Spreadsheet;

namespace DocumentManagement.Services;

public sealed class PdvtService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement", "PDVT");
    private static string LayoutsPath => Path.Combine(Folder, "boq-layouts.json");

    /// <summary>Column choices the user confirmed for a header layout (keyed by <see cref="BoqWorkbook.Signature"/>).</summary>
    public BoqLayout? FindLayout(string signature)
    {
        if (string.IsNullOrEmpty(signature)) return null;
        return LoadLayouts().TryGetValue(signature, out var layout) ? layout : null;
    }

    public void SaveLayout(string signature, BoqColumnMap map)
    {
        if (string.IsNullOrEmpty(signature)) return;
        var layouts = LoadLayouts();
        layouts[signature] = map.ToLayout();
        Directory.CreateDirectory(Folder);
        File.WriteAllText(LayoutsPath, JsonSerializer.Serialize(layouts, JsonOptions));
    }

    private static Dictionary<string, BoqLayout> LoadLayouts()
    {
        if (!File.Exists(LayoutsPath)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, BoqLayout>>(File.ReadAllText(LayoutsPath), JsonOptions) ?? []; }
        catch (JsonException) { return []; } // a corrupt file is rewritten on the next save
    }
    private static string FilePath(int documentId) => Path.Combine(Folder, $"{documentId}.json");
    private static string BoqFilePath(int documentId) => Path.Combine(Folder, $"{documentId}.boq.json");
    public List<PdvtRecord> Load(int documentId) => File.Exists(FilePath(documentId)) ? JsonSerializer.Deserialize<List<PdvtRecord>>(File.ReadAllText(FilePath(documentId)), JsonOptions) ?? [] : [];
    public void Save(int documentId, IEnumerable<PdvtRecord> records) { Directory.CreateDirectory(Folder); File.WriteAllText(FilePath(documentId), JsonSerializer.Serialize(records, JsonOptions)); }
    public List<PdvtBoqItem> LoadBoq(int documentId)
    {
        var items = File.Exists(BoqFilePath(documentId)) ? JsonSerializer.Deserialize<List<PdvtBoqItem>>(File.ReadAllText(BoqFilePath(documentId)), JsonOptions) ?? [] : [];
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.NhanHieuTheoHd)) continue;
            var sourceOptions = item.NhanHieuOptions.Count > 0 ? item.NhanHieuOptions : ParseOptions(item.NhanHieuXuatXu);
            var brands = new List<string>();
            foreach (var option in sourceOptions)
            {
                var split = SplitBrandOrigin(option, string.Empty);
                if (string.IsNullOrWhiteSpace(item.XuatXuTheoHd)) item.XuatXuTheoHd = split.Origin;
                brands.AddRange(split.Options);
            }
            item.NhanHieuOptions = brands.Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
            item.NhanHieuTheoHd = string.Join(" / ", item.NhanHieuOptions);
        }
        return items;
    }
    public void SaveBoq(int documentId, IEnumerable<PdvtBoqItem> items) { Directory.CreateDirectory(Folder); File.WriteAllText(BoqFilePath(documentId), JsonSerializer.Serialize(items, JsonOptions)); }
    public static int NextNumber(IEnumerable<PdvtRecord> records) => records.Select(x => System.Text.RegularExpressions.Regex.Match(x.Number, @"^PDVT[- ]0*(\d+)(?:_v\d+)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).Where(x => x.Success).Select(x => int.Parse(x.Groups[1].Value)).DefaultIfEmpty().Max() + 1;
    public string ExportExcel(PdvtRecord record, Document document, CvxdInfo? projectInfo = null, string? templatePath = null)
    {
        if (string.IsNullOrWhiteSpace(document.FolderPath) || !Directory.Exists(document.FolderPath))
            throw new DirectoryNotFoundException("Không tìm thấy thư mục hợp đồng để lưu PDVT.");
        var outputFolder = Path.Combine(document.FolderPath, "3.HSCL", "PDVT");
        Directory.CreateDirectory(outputFolder);
        var filePath = Path.Combine(outputFolder, $"{record.Number}.xlsx");
        var externalTemplate = Path.Combine(AppContext.BaseDirectory, "Templates", "Pdvt", "PDVT_template.xlsx");
        using var templateStream = templatePath != null ? File.OpenRead(templatePath)
            : File.Exists(externalTemplate) ? File.OpenRead(externalTemplate)
            : typeof(PdvtService).Assembly.GetManifestResourceStream("DocumentManagement.Templates.Pdvt.PDVT_template.xlsx");
        if (templateStream == null) throw new FileNotFoundException("Không tìm thấy mẫu Excel PDVT trong app.");
        using var book = new XLWorkbook(templateStream);
        var sheet = book.Worksheet("pdvt 01");
        static string Prefer(string? preferred, string? fallback) => string.IsNullOrWhiteSpace(preferred) ? fallback ?? string.Empty : preferred;
        foreach (var other in book.Worksheets.Where(x => x != sheet).ToList()) other.Delete();
        sheet.Name = record.Number.Length > 31 ? record.Number[..31] : record.Number;
        var category = string.IsNullOrWhiteSpace(record.HạngMục) ? document.ItemName : record.HạngMục;
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{SoPDVT}"] = record.Number, ["{NgayLap}"] = record.Date.ToString("dd/MM/yyyy"),
            ["{SoHD}"] = document.ContractNumber, ["{HangMuc}"] = category,
            ["{TenDuAn}"] = Prefer(projectInfo?.TenDuAn, document.ProjectName),
            ["{DuAn}"] = Prefer(projectInfo?.TenDuAn, document.ProjectName),
            ["{ChuDauTu}"] = Prefer(projectInfo?.TenChuDauTu, document.InvestorName),
            ["{TenCongTrinh}"] = Prefer(projectInfo?.TenCongTrinh, document.ConstructionName),
            ["{CongTrinh}"] = Prefer(projectInfo?.TenCongTrinh, document.ConstructionName),
            ["{DiaDiem}"] = Prefer(projectInfo?.DiaDiem, document.Location),
            ["{Diadiem}"] = Prefer(projectInfo?.DiaDiem, document.Location),
            ["{TenNTC}"] = Prefer(projectInfo?.TenChuDauTu, document.EmployerName),
            ["{DaiDienNTC}"] = projectInfo?.DaiDienChuDauTu ?? string.Empty,
            ["{ChucVuNTC}"] = projectInfo?.ChucVuCDT ?? string.Empty,
            ["{ChucvuNTC}"] = projectInfo?.ChucVuCDT ?? string.Empty,
            ["{TenNTP}"] = Prefer(projectInfo?.TenNhaThau, document.ContractorName),
            ["{DaiDienNTP}"] = projectInfo?.DaiDienNhaThau ?? string.Empty,
            ["{ChucVuNTP}"] = projectInfo?.ChucVuNT ?? string.Empty,
            ["{ChucvuNTP}"] = projectInfo?.ChucVuNT ?? string.Empty,
            ["{TenTVGS}"] = projectInfo?.TenTuVanGiamSat ?? string.Empty,
            ["{DaiDienTVGS}"] = projectInfo?.DaiDienTVGS ?? string.Empty,
            ["{ChucVuTVGS}"] = projectInfo?.ChucVuTVGS ?? string.Empty,
            ["{ChucvuTVGS}"] = projectInfo?.ChucVuTVGS ?? string.Empty
        };
        foreach (var cell in sheet.CellsUsed().Where(cell => !cell.HasFormula))
        {
            var value = cell.GetString();
            if (!value.Contains('{')) continue;
            foreach (var replacement in replacements) value = value.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
            cell.Value = value;
        }
        var materialCount = record.Materials.Count;
        if (materialCount == 0) sheet.Row(15).Delete();
        else if (materialCount > 1) sheet.Row(16).InsertRowsAbove(materialCount - 1);
        for (var index = 0; index < materialCount; index++)
        {
            var rowNumber = 15 + index;
            if (index > 0) sheet.Row(15).CopyTo(sheet.Row(rowNumber));
            var material = record.Materials[index];
            sheet.Cell(rowNumber, 1).Value = index + 1;
            sheet.Cell(rowNumber, 2).Value = material.NoiDungTheoHd;
            sheet.Cell(rowNumber, 3).Value = string.IsNullOrWhiteSpace(material.ModelTheoHd) ? material.ThongSoTheoHd : material.ModelTheoHd + "\n" + material.ThongSoTheoHd;
            sheet.Cell(rowNumber, 4).Value = material.NhanHieuTheoHd;
            sheet.Cell(rowNumber, 5).Value = material.XuatXuTheoHd;
            sheet.Cell(rowNumber, 6).Value = material.NoiDung;
            sheet.Cell(rowNumber, 7).Value = string.IsNullOrWhiteSpace(material.Model) ? material.ThongSoKyThuat : material.Model + "\n" + material.ThongSoKyThuat;
            sheet.Cell(rowNumber, 8).Value = material.NhanHieuPheDuyet;
            sheet.Cell(rowNumber, 9).Value = material.XuatXuDeTrinh;
            sheet.Cell(rowNumber, 10).Value = material.Note;

            for (var col = 1; col <= 10; col++)
            {
                sheet.Cell(rowNumber, col).Style.Alignment.WrapText = true;
                sheet.Cell(rowNumber, col).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
            sheet.Row(rowNumber).Height = CalculateAutoRowHeight(sheet, rowNumber);
        }
        book.SaveAs(filePath);
        return filePath;
    }

    /// <summary>Writes the BoQ approval summary (one row per BoQ line, headings kept) next to the PDVT sheets.</summary>
    public string ExportSummary(IEnumerable<PdvtBoqItem> items, Document document)
    {
        if (string.IsNullOrWhiteSpace(document.FolderPath) || !Directory.Exists(document.FolderPath))
            throw new DirectoryNotFoundException("Không tìm thấy thư mục hợp đồng để lưu bảng tổng hợp.");
        var outputFolder = Path.Combine(document.FolderPath, "3.HSCL", "PDVT");
        Directory.CreateDirectory(outputFolder);
        var filePath = Path.Combine(outputFolder, "Tong hop phe duyet vat tu.xlsx");

        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Tổng hợp PDVT");
        sheet.Cell(1, 1).Value = "BẢNG TỔNG HỢP PHÊ DUYỆT VẬT TƯ";
        sheet.Cell(2, 1).Value = $"Hợp đồng: {document.ContractNumber}";
        sheet.Range(1, 1, 1, 11).Merge().Style.Font.SetBold().Font.SetFontSize(14);
        string[] headers = ["STT", "Nội dung vật tư", "Model · thông số kỹ thuật theo HĐ", "Nhãn hiệu / xuất xứ theo HĐ", "ĐVT", "Khối lượng",
            "Trạng thái", "Nhãn hiệu được duyệt", "Xuất xứ được duyệt", "Phiếu PDVT", "Ngày duyệt"];
        const int headerRow = 4;
        for (var col = 0; col < headers.Length; col++) sheet.Cell(headerRow, col + 1).Value = headers[col];
        var header = sheet.Range(headerRow, 1, headerRow, headers.Length);
        header.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#D7E1EC"));

        var row = headerRow + 1;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.SttBoq;
            sheet.Cell(row, 2).Value = item.NoiDung;
            if (item.IsHeading)
            {
                sheet.Cell(row, 7).Value = $"{item.ChildApproved}/{item.ChildTotal} đã duyệt";
                sheet.Range(row, 1, row, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#EEF2F7"));
            }
            else
            {
                sheet.Cell(row, 3).Value = item.SpecWithModel;
                sheet.Cell(row, 4).Value = item.BrandOriginDisplay;
                sheet.Cell(row, 5).Value = item.DonViTinh;
                sheet.Cell(row, 6).Value = item.KhoiLuong;
                sheet.Cell(row, 7).Value = item.StatusLabel;
                sheet.Cell(row, 8).Value = item.ApprovedBrand;
                sheet.Cell(row, 9).Value = item.ApprovedOrigin;
                sheet.Cell(row, 10).Value = item.LatestReference;
                if (item.ApprovedDate is { } date) sheet.Cell(row, 11).Value = date.ToString("dd/MM/yyyy");
            }
            row++;
        }

        var table = sheet.Range(headerRow, 1, Math.Max(headerRow, row - 1), headers.Length);
        table.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        table.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetWrapText(true);
        double[] widths = [8, 40, 34, 26, 8, 12, 16, 22, 16, 16, 12];
        for (var col = 0; col < widths.Length; col++) sheet.Column(col + 1).Width = widths[col];
        sheet.Column(6).Style.NumberFormat.Format = "#,##0.00";
        sheet.SheetView.FreezeRows(headerRow);
        book.SaveAs(filePath);
        return filePath;
    }

    private static double CalculateAutoRowHeight(IXLWorksheet sheet, int rowNumber)
    {
        var maxLines = 1;
        for (var col = 2; col <= 10; col++)
        {
            var text = sheet.Cell(rowNumber, col).GetString();
            if (string.IsNullOrWhiteSpace(text)) continue;

            var colWidth = sheet.Column(col).Width;
            var charsPerLine = Math.Max(12, (int)Math.Floor((colWidth - 2) * 1.05));
            var explicitLines = text.Replace("\r\n", "\n").Split('\n');
            var totalLinesForCell = 0;
            foreach (var line in explicitLines)
            {
                var wrapped = Math.Max(1, (int)Math.Ceiling((double)line.Trim().Length / charsPerLine));
                totalLinesForCell += wrapped;
            }
            if (totalLinesForCell > maxLines) maxLines = totalLinesForCell;
        }
        return Math.Max(26, maxLines * 16.5 + 10);
    }
    public static List<string> ParseOptions(string value)
    {
        // A multiline value with labelled components (e.g. "Vỏ tủ: ...\nThiết bị: ...")
        // describes one specification, not alternative brands.
        if (value.Contains(':') && !value.Contains(';') && !value.Contains('|'))
            return string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];
        return value.Split([';', '\n', '|', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    public static (string Brand, string Origin, List<string> Options) SplitBrandOrigin(string brand, string origin)
    {
        var parts = ParseOptions(brand);
        if (parts.Count > 1 && !string.IsNullOrWhiteSpace(origin) &&
            string.Equals(parts[^1], origin, StringComparison.CurrentCultureIgnoreCase))
            parts.RemoveAt(parts.Count - 1);
        if (string.IsNullOrWhiteSpace(origin) && parts.Count > 1 &&
            new[] { "Việt Nam", "Viet Nam", "Vietnam", "Trung Quốc", "China", "Nhật Bản", "Japan", "Hàn Quốc", "Korea", "Thái Lan", "Thailand" }
                .Contains(parts[^1], StringComparer.CurrentCultureIgnoreCase))
        {
            origin = parts[^1];
            parts.RemoveAt(parts.Count - 1);
        }
        return (brand, origin, parts);
    }
    /// <summary>Reads a BoQ with automatically detected columns (no dialog).</summary>
    public static List<PdvtBoqItem> ReadBoq(string path)
    {
        using var workbook = BoqWorkbook.Open(path);
        var map = workbook.Detect();
        if (map.Get(BoqField.NoiDung) == 0)
            throw new InvalidDataException($"Không tìm thấy dòng tiêu đề có cột tên/nội dung vật tư trong các sheet: {string.Join(", ", workbook.SheetNames)}. Hãy kiểm tra sheet và tên cột BoQ.");
        return workbook.Read(map);
    }

    internal static string MakeExternalFormulaValuesReadable(string sourcePath)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"pdvt-boq-{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}");
        File.Copy(sourcePath, tempPath);
        try
        {
            // Some contract workbooks contain broken external file hyperlinks.
            // They are discarded below, so give malformed targets a valid inert
            // URI long enough for OpenXML to open the temporary copy.
            var settings = new OpenSettings
            {
                RelationshipErrorHandlerFactory = RelationshipErrorHandler.CreateRewriterFactory(
                    (partUri, id, uri) => "file:///invalid-external-reference")
            };
            using var document = SpreadsheetDocument.Open(tempPath, true, settings);
            var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("Workbook không có cấu trúc Excel hợp lệ.");
            foreach (var worksheetPart in workbookPart.WorksheetParts)
            {
                foreach (var cell in worksheetPart.Worksheet.Descendants<Spreadsheet.Cell>())
                {
                    if (cell.CellFormula?.Text?.Contains('[', StringComparison.Ordinal) == true)
                        cell.CellFormula.Remove(); // Preserve Excel's last cached value in <v>.
                }
                worksheetPart.Worksheet.Save();
            }

            if (workbookPart.Workbook.DefinedNames != null)
            {
                foreach (var name in workbookPart.Workbook.DefinedNames.Elements<Spreadsheet.DefinedName>()
                             .Where(name => name.Text?.Contains('[', StringComparison.Ordinal) == true).ToList())
                    name.Remove();
            }
            workbookPart.Workbook.ExternalReferences?.Remove();
            foreach (var externalWorkbookPart in workbookPart.ExternalWorkbookParts.ToList())
                workbookPart.DeletePart(externalWorkbookPart);
            workbookPart.Workbook.Save();
            return tempPath;
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
    }
}
