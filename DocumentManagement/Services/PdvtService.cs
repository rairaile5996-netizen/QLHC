using System.Text.Json;
using System.IO;
using ClosedXML.Excel;
using DocumentManagement.Models;
using DocumentFormat.OpenXml.Packaging;
using Spreadsheet = DocumentFormat.OpenXml.Spreadsheet;

namespace DocumentManagement.Services;

public sealed class PdvtService
{
    private static readonly string[][] BoqHeaderAliases =
    [
        ["Nội dung công việc", "Nội dung", "Tên công việc", "Tên vật tư", "Tên vật liệu", "Tên hàng hóa", "Tên hàng hóa thiết bị", "Tên vật tư thiết bị", "Tên thiết bị vật tư", "Vật tư thiết bị", "Tên thiết bị", "Tên sản phẩm", "Diễn giải", "Mô tả", "Hạng mục công việc", "Danh mục vật tư", "Tên vật tư / quy cách"],
        ["Thông số kỹ thuật", "Thông số", "Yêu cầu kỹ thuật", "Đặc tính kỹ thuật", "Quy cách", "Quy cách kỹ thuật", "Thông số kỹ thuật / quy cách", "Quy cách - thông số", "Quy cách / Thông số kỹ thuật", "Tiêu chuẩn kỹ thuật", "Thông số KT", "TSKT", "Mô tả kỹ thuật", "Kích thước", "Yêu cầu vật tư", "Quy cách vật tư", "Chủng loại / quy cách", "Mô tả quy cách"],
        ["Nhãn hiệu - xuất xứ", "Nhãn hiệu/xuất xứ", "Nhãn hiệu xuất xứ", "Thương hiệu / xuất xứ", "Hãng / xuất xứ", "Nhãn hiệu", "Thương hiệu", "Hãng sản xuất", "Nhà sản xuất", "Hãng", "Hãng SX", "Nhà SX"],
        ["Model", "Mã hiệu", "Mã sản phẩm", "Mã vật tư", "Chủng loại", "Ký hiệu"],
        ["Đơn vị tính", "ĐVT", "Đơn vị", "Đơn vị đo"],
        ["Đơn giá", "Giá", "Đơn giá vật tư", "Đơn giá hợp đồng"],
        ["Khối lượng", "Số lượng", "KL", "SL", "Khối lượng theo HĐ", "Số lượng theo HĐ"],
        ["Xuất xứ", "Nước sản xuất", "Nguồn gốc", "Nước xuất xứ", "Quốc gia xuất xứ"]
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement", "PDVT");
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
    public static List<PdvtBoqItem> ReadBoq(string path)
    {
        var readablePath = MakeExternalFormulaValuesReadable(path);
        try
        {
            using var book = new XLWorkbook(readablePath);
            var candidate = book.Worksheets
                .SelectMany(sheet => FindHeaderRows(sheet))
                .OrderByDescending(header => header.Score)
                .FirstOrDefault();
            if (candidate.Sheet is null)
            {
                var sheetNames = string.Join(", ", book.Worksheets.Select(sheet => sheet.Name));
                throw new InvalidDataException($"Không tìm thấy dòng tiêu đề có cột tên/nội dung vật tư trong các sheet: {sheetNames}. Hãy kiểm tra sheet và tên cột BoQ.");
            }

            var rows = new List<PdvtBoqItem>();
            var contentColumn = candidate.Columns[0].SelectMany(group => group).First();
            var numberColumn = contentColumn > 1 ? contentColumn - 1 : 0;
            var currentSection = string.Empty;
            var lastRow = candidate.Sheet.RangeUsed()!.RangeAddress.LastAddress.RowNumber;
            for (var rowNumber = candidate.RowNumber + 1; rowNumber <= lastRow; rowNumber++)
            {
                string Get(int fieldIndex) => candidate.Columns[fieldIndex]
                    .SelectMany(column => column)
                    .Select(column => candidate.Sheet.Cell(rowNumber, column).GetString().Trim())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

                var brand = Get(2);
                var origin = Get(7);
                var split = SplitBrandOrigin(brand, origin);
                var brandOptions = split.Options;
                var approvedOptions = string.IsNullOrWhiteSpace(split.Origin)
                    ? brandOptions
                    : brandOptions.Count == 0
                        ? [split.Origin]
                        : brandOptions.Select(option => option.Contains(split.Origin, StringComparison.CurrentCultureIgnoreCase)
                            ? option : $"{option} / {split.Origin}").ToList();
                var item = new PdvtBoqItem
                {
                    SttBoq = numberColumn == 0 ? string.Empty : candidate.Sheet.Cell(rowNumber, numberColumn).GetString().Trim(),
                    NoiDung = Get(0),
                    ThongSoKyThuat = Get(1),
                    NhanHieuXuatXu = string.Join("; ", approvedOptions),
                    NhanHieuTheoHd = brand,
                    XuatXuTheoHd = split.Origin,
                    Model = Get(3),
                    DonViTinh = Get(4),
                    DonGia = DecimalParse(Get(5)),
                    KhoiLuong = DecimalParse(Get(6))
                };
                if (string.IsNullOrWhiteSpace(item.NoiDung)) continue;
                if (item.KhoiLuong == 0 && item.DonGia == 0 &&
                    (string.IsNullOrWhiteSpace(item.DonViTinh) || item.DonViTinh == "0"))
                {
                    if (string.IsNullOrWhiteSpace(item.SttBoq) ||
                        item.NoiDung.StartsWith("Tổng", StringComparison.CurrentCultureIgnoreCase)) continue;
                    item.IsHeading = true;
                    item.HeadingLevel = item.SttBoq.Count(c => c == '.') + 1;
                    currentSection = item.SttBoq;
                }
                else item.SectionCode = currentSection;
                item.NhanHieuOptions = brandOptions;
                rows.Add(item);
            }

            return rows;
        }
        finally
        {
            if (!string.Equals(readablePath, path, StringComparison.OrdinalIgnoreCase))
                File.Delete(readablePath);
        }
    }

    private static string MakeExternalFormulaValuesReadable(string sourcePath)
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

    private static IEnumerable<(IXLWorksheet Sheet, int RowNumber, int Score, List<List<List<int>>> Columns)> FindHeaderRows(IXLWorksheet sheet)
    {
        var used = sheet.RangeUsed();
        if (used == null) yield break;

        var firstRow = used.RangeAddress.FirstAddress.RowNumber;
        var lastRow = Math.Min(used.RangeAddress.LastAddress.RowNumber, firstRow + 59);
        for (var rowNumber = firstRow; rowNumber <= lastRow; rowNumber++)
        {
            var headers = sheet.Row(rowNumber).CellsUsed()
                .Select(cell => (Name: Normalize(cell.GetString()), Column: cell.Address.ColumnNumber))
                .Where(header => !string.IsNullOrWhiteSpace(header.Name))
                .GroupBy(header => header.Name)
                .ToDictionary(group => group.Key, group => group.Select(header => header.Column).ToList());

            var columns = BoqHeaderAliases
                .Select(aliases => aliases.Select(Normalize).Where(headers.ContainsKey).Select(key => headers[key]).ToList())
                .ToList();
            if (columns[0].Count == 0) continue;

            var mappedFields = columns.Count(field => field.Count > 0);
            yield return (sheet, rowNumber, mappedFields, columns);
        }
    }
    private static string Normalize(string s)
    {
        var withoutMarks = string.Concat(s.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark));
        return string.Concat(withoutMarks.Replace("đ", "d", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit));
    }
    private static decimal DecimalParse(string s) => decimal.TryParse(s.Replace(",", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;
}
