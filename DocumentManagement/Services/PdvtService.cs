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
        var templateRow = FindMaterialRow(sheet);
        if (templateRow == 0) WriteFixedLayout(sheet, record.Materials);
        else WriteTemplateRows(sheet, templateRow, record.Materials);
        book.SaveAs(filePath);
        return filePath;
    }

    /// <summary>
    /// Per-material placeholders for the template row. Put them in any cell of one row; that row is repeated once
    /// per material. "…HD" = theo hợp đồng, without suffix = đệ trình.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Func<PdvtMaterialRow, int, object>> RowPlaceholders =
        new Dictionary<string, Func<PdvtMaterialRow, int, object>>(StringComparer.Ordinal)
        {
            ["{STT}"] = SttValue,
            ["{NoiDungHD}"] = (m, _) => m.NoiDungTheoHd,
            ["{ModelThongSoHD}"] = (m, _) => JoinLines(m.ModelTheoHd, m.ThongSoTheoHd),
            ["{ModelHD}"] = (m, _) => m.ModelTheoHd,
            ["{ThongSoHD}"] = (m, _) => m.ThongSoTheoHd,
            ["{NhanHieuHD}"] = (m, _) => m.NhanHieuTheoHd,
            ["{XuatXuHD}"] = (m, _) => m.XuatXuTheoHd,
            ["{NoiDung}"] = (m, _) => m.NoiDung,
            ["{ModelThongSo}"] = (m, _) => JoinLines(m.Model, m.ThongSoKyThuat),
            ["{Model}"] = (m, _) => m.Model,
            ["{ThongSo}"] = (m, _) => m.ThongSoKyThuat,
            ["{NhanHieu}"] = (m, _) => m.NhanHieuPheDuyet,
            ["{XuatXu}"] = (m, _) => m.XuatXuDeTrinh,
            ["{DVT}"] = (m, _) => m.DonViTinh,
            ["{KhoiLuong}"] = (m, _) => m.KhoiLuong,
            ["{DonGia}"] = (m, _) => m.DonGia,
            ["{ThanhTien}"] = (m, _) => m.DonGia * m.KhoiLuong,
            ["{GhiChu}"] = (m, _) => m.Note,
            ["{KetQua}"] = (m, _) => m.Status == PdvtApprovalStatus.ChuaTrinh ? string.Empty : m.StatusLabel,
            ["{YKienTVGS}"] = (m, _) => m.ReviewNote
        };

    private static string JoinLines(string first, string second) =>
        string.Join("\n", new[] { first, second }.Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>First row that contains a per-material placeholder, or 0.</summary>
    public static int FindMaterialRow(IXLWorksheet sheet) =>
        sheet.CellsUsed(c => !c.HasFormula && RowPlaceholders.Keys.Any(key => c.GetString().Contains(key, StringComparison.Ordinal)))
            .Select(c => c.Address.RowNumber).DefaultIfEmpty(0).Min();

    /// <summary>One output row: a BoQ heading, or a material with its STT counter inside its section.</summary>
    public sealed record PdvtSheetLine(PdvtSectionRef? Heading, PdvtMaterialRow? Material, int Index);

    /// <summary>
    /// Materials in sheet order, each preceded by the BoQ headings that changed since the previous material, so the
    /// reader sees which section ("III. Bơm chữa cháy các loại") a line belongs to.
    /// </summary>
    public static List<PdvtSheetLine> BuildSheetLines(IReadOnlyList<PdvtMaterialRow> materials)
    {
        var lines = new List<PdvtSheetLine>();
        var grouped = materials.Any(m => m.Sections.Count > 0);
        List<PdvtSectionRef> previous = [];
        var counter = 0;
        foreach (var material in materials)
        {
            List<PdvtSectionRef> sections = material.Sections.Count > 0 || !grouped ? material.Sections
                : [new PdvtSectionRef(string.Empty, material.IsExtra ? "Phát sinh ngoài BoQ" : "Vật tư theo BoQ")];
            var same = 0;
            while (same < sections.Count && same < previous.Count && sections[same] == previous[same]) same++;
            if (same < sections.Count || sections.Count != previous.Count) counter = 0;
            for (var level = same; level < sections.Count; level++) lines.Add(new PdvtSheetLine(sections[level], null, 0));
            previous = sections;
            lines.Add(new PdvtSheetLine(null, material, ++counter));
        }
        return lines;
    }

    /// <summary>BoQ STT when the line came from the BoQ, otherwise the running number inside its section.</summary>
    private static object SttValue(PdvtMaterialRow material, int index) =>
        string.IsNullOrWhiteSpace(material.SttBoq) ? index
        : int.TryParse(material.SttBoq, out var number) ? number : material.SttBoq;

    private static void WriteTemplateRows(IXLWorksheet sheet, int templateRow, IReadOnlyList<PdvtMaterialRow> materials)
    {
        if (materials.Count == 0) { sheet.Row(templateRow).Delete(); return; }
        var lines = BuildSheetLines(materials);
        // Columns of the template row, captured before rows are inserted.
        var lastColumn = Math.Max(sheet.Row(templateRow).LastCellUsed()?.Address.ColumnNumber ?? 1,
                                  sheet.LastColumnUsed()?.ColumnNumber() ?? 1);
        var templates = Enumerable.Range(1, lastColumn)
            .Select(column => (Column: column, Text: sheet.Cell(templateRow, column).HasFormula ? string.Empty : sheet.Cell(templateRow, column).GetString()))
            .Where(x => x.Text.Contains('{'))
            .ToList();
        var templateHeight = sheet.Row(templateRow).Height;
        var templateBold = Enumerable.Range(1, lastColumn).Select(c => sheet.Cell(templateRow, c).Style.Font.Bold).ToArray();
        if (lines.Count > 1) sheet.Row(templateRow).InsertRowsBelow(lines.Count - 1);

        // Bottom-up: the template row is copied while still untouched (a heading written to it first would make every copy bold).
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            var rowNumber = templateRow + index;
            if (index > 0) sheet.Row(templateRow).CopyTo(sheet.Row(rowNumber));
            var line = lines[index];
            if (line.Heading is { } heading)
            {
                WriteHeadingRow(sheet, rowNumber, lastColumn, templates, heading);
                continue;
            }
            var material = line.Material!;
            foreach (var (column, text) in templates)
            {
                var cell = sheet.Cell(rowNumber, column);
                var whole = text.Trim();
                // A cell holding just one numeric placeholder keeps a real number so Excel formulas can use it.
                if (RowPlaceholders.TryGetValue(whole, out var single) && single(material, line.Index) is var value and (int or decimal))
                {
                    cell.Value = value is int i ? i : (decimal)value;
                    continue;
                }
                var result = text;
                foreach (var (key, getter) in RowPlaceholders)
                    if (result.Contains(key, StringComparison.Ordinal))
                        result = result.Replace(key, FormatValue(getter(material, line.Index)), StringComparison.Ordinal);
                cell.Value = result;
            }
            sheet.Row(rowNumber).Height = CalculateAutoRowHeight(sheet, rowNumber, lastColumn, templateHeight);
        }
        ExpandTemplateRanges(sheet, templateRow, templateRow + lines.Count - 1);
        // Font weight last, once every row exists: copies made from the template row otherwise pick up a heading's bold.
        for (var index = 0; index < lines.Count; index++)
            for (var c = 1; c <= lastColumn; c++)
                sheet.Cell(templateRow + index, c).Style.Font.Bold = lines[index].Heading != null || templateBold[c - 1];
    }

    /// <summary>Heading code goes where {STT} is, its name where the content placeholders are; other cells stay empty.</summary>
    private static void WriteHeadingRow(IXLWorksheet sheet, int rowNumber, int lastColumn, List<(int Column, string Text)> templates, PdvtSectionRef heading)
    {
        foreach (var (column, text) in templates)
        {
            sheet.Cell(rowNumber, column).Value = text.Contains("{STT}", StringComparison.Ordinal) ? heading.Code
                : text.Contains("{NoiDungHD}", StringComparison.Ordinal) || text.Contains("{NoiDung}", StringComparison.Ordinal) ? heading.Name
                : string.Empty;
        }
        sheet.Row(rowNumber).Height = CalculateAutoRowHeight(sheet, rowNumber, lastColumn, 0);
    }

    /// <summary>
    /// Totals below the list usually reference just the template row (SUM(D15:D15)); Excel only grows a range when
    /// rows are inserted inside it, so stretch such ranges over every material row.
    /// </summary>
    private static void ExpandTemplateRanges(IXLWorksheet sheet, int templateRow, int lastRow)
    {
        if (lastRow <= templateRow) return;
        var pattern = new System.Text.RegularExpressions.Regex($@"(\$?[A-Z]{{1,3}}\$?){templateRow}:(\$?[A-Z]{{1,3}}\$?){templateRow}(?!\d)");
        foreach (var cell in sheet.CellsUsed(c => c.HasFormula && c.Address.RowNumber > lastRow))
        {
            var formula = cell.FormulaA1;
            var expanded = pattern.Replace(formula, m => $"{m.Groups[1].Value}{templateRow}:{m.Groups[2].Value}{lastRow}");
            if (expanded != formula) cell.FormulaA1 = expanded;
        }
    }

    private static string FormatValue(object value) => value switch
    {
        decimal d => d.ToString(d == Math.Truncate(d) ? "#,##0" : "#,##0.##", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>Layout of templates without row placeholders: row 15, columns A–J.</summary>
    private static void WriteFixedLayout(IXLWorksheet sheet, IReadOnlyList<PdvtMaterialRow> materials)
    {
        if (materials.Count == 0) { sheet.Row(15).Delete(); return; }
        var lines = BuildSheetLines(materials);
        if (lines.Count > 1) sheet.Row(16).InsertRowsAbove(lines.Count - 1);
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            var rowNumber = 15 + index;
            if (index > 0) sheet.Row(15).CopyTo(sheet.Row(rowNumber));
            for (var col = 1; col <= 10; col++)
            {
                sheet.Cell(rowNumber, col).Style.Alignment.WrapText = true;
                sheet.Cell(rowNumber, col).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
            var line = lines[index];
            if (line.Heading is { } heading)
            {
                sheet.Cell(rowNumber, 1).Value = heading.Code;
                sheet.Cell(rowNumber, 2).Value = heading.Name;
                sheet.Cell(rowNumber, 6).Value = heading.Name;
                sheet.Range(rowNumber, 1, rowNumber, 10).Style.Font.Bold = true;
                sheet.Row(rowNumber).Height = CalculateAutoRowHeight(sheet, rowNumber, 10, 0);
                continue;
            }
            var material = line.Material!;
            sheet.Range(rowNumber, 1, rowNumber, 10).Style.Font.Bold = false;
            var stt = SttValue(material, line.Index);
            if (stt is int number) sheet.Cell(rowNumber, 1).Value = number;
            else sheet.Cell(rowNumber, 1).Value = stt.ToString();
            sheet.Cell(rowNumber, 2).Value = material.NoiDungTheoHd;
            sheet.Cell(rowNumber, 3).Value = JoinLines(material.ModelTheoHd, material.ThongSoTheoHd);
            sheet.Cell(rowNumber, 4).Value = material.NhanHieuTheoHd;
            sheet.Cell(rowNumber, 5).Value = material.XuatXuTheoHd;
            sheet.Cell(rowNumber, 6).Value = material.NoiDung;
            sheet.Cell(rowNumber, 7).Value = JoinLines(material.Model, material.ThongSoKyThuat);
            sheet.Cell(rowNumber, 8).Value = material.NhanHieuPheDuyet;
            sheet.Cell(rowNumber, 9).Value = material.XuatXuDeTrinh;
            sheet.Cell(rowNumber, 10).Value = material.Note;
            sheet.Row(rowNumber).Height = CalculateAutoRowHeight(sheet, rowNumber, 10, 26);
        }
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

    private static double CalculateAutoRowHeight(IXLWorksheet sheet, int rowNumber, int lastColumn, double minHeight)
    {
        var maxLines = 1;
        for (var col = 1; col <= lastColumn; col++)
        {
            var text = sheet.Cell(rowNumber, col).GetString();
            if (string.IsNullOrWhiteSpace(text)) continue;
            // Merged cells span several columns; measure against the whole width.
            var merged = sheet.Cell(rowNumber, col).MergedRange();
            var colWidth = merged == null ? sheet.Column(col).Width
                : Enumerable.Range(merged.FirstColumn().ColumnNumber(), merged.ColumnCount()).Sum(c => sheet.Column(c).Width);
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
        return Math.Max(Math.Max(26, minHeight > 0 ? Math.Min(minHeight, 60) : 26), maxLines * 16.5 + 10);
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
