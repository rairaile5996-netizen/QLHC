using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentManagement.Models;

namespace DocumentManagement.Services;

public enum BoqField { Stt, NoiDung, ThongSo, Model, NhanHieu, XuatXu, DonVi, KhoiLuong, DonGia }

/// <summary>Which sheet column holds each BoQ field (0 = not present).</summary>
public sealed class BoqColumnMap
{
    public string SheetName { get; set; } = string.Empty;
    public int HeaderRow { get; set; }
    public Dictionary<BoqField, int> Columns { get; set; } = [];
    /// <summary>Further columns with the same header (repeated headings); read when the primary cell is empty.</summary>
    public Dictionary<BoqField, List<int>> Fallbacks { get; set; } = [];

    public int Get(BoqField field) => Columns.TryGetValue(field, out var column) ? column : 0;

    public IEnumerable<int> All(BoqField field)
    {
        var primary = Get(field);
        if (primary == 0) yield break;
        yield return primary;
        if (Fallbacks.TryGetValue(field, out var more))
            foreach (var column in more.Where(c => c != primary)) yield return column;
    }

    public void Set(BoqField field, int column)
    {
        if (Get(field) != column) Fallbacks.Remove(field);
        if (column > 0) Columns[field] = column;
        else Columns.Remove(field);
    }

    /// <summary>Applies saved/user choices; detected fallbacks survive only for unchanged fields.</summary>
    public void Apply(IReadOnlyDictionary<BoqField, int> columns)
    {
        foreach (var field in Enum.GetValues<BoqField>()) Set(field, columns.TryGetValue(field, out var c) ? c : 0);
    }

    public void Apply(BoqLayout layout)
    {
        Apply(layout.Columns);
        foreach (var (field, column) in layout.Fallbacks) SetFallback(field, column);
    }

    /// <summary>Column read when the primary cell is empty (e.g. brand sometimes in "Nhãn hiệu", sometimes in "Mã hiệu").</summary>
    public void SetFallback(BoqField field, int column)
    {
        if (column > 0 && column != Get(field)) Fallbacks[field] = [column];
        else Fallbacks.Remove(field);
    }

    public int GetFallback(BoqField field) => Fallbacks.TryGetValue(field, out var more) ? more.FirstOrDefault(c => c != Get(field)) : 0;

    public BoqLayout ToLayout() => new()
    {
        Columns = new Dictionary<BoqField, int>(Columns),
        Fallbacks = Fallbacks.Where(x => x.Value.Count > 0).ToDictionary(x => x.Key, x => x.Value[0])
    };
}

/// <summary>Persisted column choices for one header template.</summary>
public sealed class BoqLayout
{
    public Dictionary<BoqField, int> Columns { get; set; } = [];
    public Dictionary<BoqField, int> Fallbacks { get; set; } = [];
}

/// <summary>A sheet column as shown in the import dialog.</summary>
public sealed record BoqColumnInfo(int Number, string Letter, string Header, string Sample)
{
    public static readonly BoqColumnInfo None = new(0, string.Empty, "— Không có —", string.Empty);
    public string Display => Number == 0 ? Header : string.IsNullOrWhiteSpace(Header) ? $"Cột {Letter}" : $"{Letter} · {Header}";
}

/// <summary>
/// An opened BoQ workbook. Detects the header row and columns by header names (any position, any sheet),
/// and reads rows with an explicit <see cref="BoqColumnMap"/> so users can correct mislabelled columns.
/// </summary>
public sealed class BoqWorkbook : IDisposable
{
    private static readonly Dictionary<BoqField, string[]> Aliases = new()
    {
        [BoqField.Stt] = ["Stt", "TT", "Số TT", "Số thứ tự", "Thứ tự", "No"],
        [BoqField.NoiDung] = ["Nội dung công việc", "Nội dung", "Tên công việc", "Tên vật tư", "Tên vật liệu", "Tên hàng hóa", "Tên hàng hóa thiết bị", "Tên vật tư thiết bị", "Tên thiết bị vật tư", "Vật tư thiết bị", "Tên thiết bị", "Tên sản phẩm", "Diễn giải", "Mô tả", "Hạng mục công việc", "Danh mục vật tư", "Tên vật tư / quy cách", "Danh mục", "Nội dung công việc / vật tư"],
        [BoqField.ThongSo] = ["Thông số kỹ thuật", "Thông số", "Yêu cầu kỹ thuật", "Đặc tính kỹ thuật", "Quy cách", "Quy cách kỹ thuật", "Thông số kỹ thuật / quy cách", "Quy cách - thông số", "Quy cách / Thông số kỹ thuật", "Tiêu chuẩn kỹ thuật", "Thông số KT", "TSKT", "Mô tả kỹ thuật", "Kích thước", "Yêu cầu vật tư", "Quy cách vật tư", "Chủng loại / quy cách", "Mô tả quy cách"],
        [BoqField.NhanHieu] = ["Nhãn hiệu - xuất xứ", "Nhãn hiệu/xuất xứ", "Nhãn hiệu xuất xứ", "Thương hiệu / xuất xứ", "Hãng / xuất xứ", "Hãng sản xuất / xuất xứ", "Nhãn hiệu", "Thương hiệu", "Hãng sản xuất", "Nhà sản xuất", "Hãng", "Hãng SX", "Nhà SX"],
        [BoqField.Model] = ["Model", "Mã hiệu", "Ký mã hiệu", "Mã sản phẩm", "Mã vật tư", "Mã hàng", "Chủng loại", "Ký hiệu", "Model / mã hiệu"],
        [BoqField.DonVi] = ["Đơn vị tính", "ĐVT", "Đơn vị", "Đơn vị đo"],
        [BoqField.DonGia] = ["Đơn giá", "Giá", "Đơn giá vật tư", "Đơn giá hợp đồng"],
        [BoqField.KhoiLuong] = ["Khối lượng", "Số lượng", "KL", "SL", "Khối lượng theo HĐ", "Số lượng theo HĐ"],
        [BoqField.XuatXu] = ["Xuất xứ", "Nước sản xuất", "Nguồn gốc", "Nước xuất xứ", "Quốc gia xuất xứ"]
    };

    private static readonly Dictionary<string, BoqField> AliasLookup = Aliases
        .SelectMany(pair => pair.Value.Select(alias => (Key: NormalizeHeader(alias), Field: pair.Key)))
        .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First().Field);

    private const int HeaderSearchRows = 60;
    private readonly XLWorkbook _book;
    private readonly string _readablePath;

    public string SourcePath { get; }
    public IReadOnlyList<string> SheetNames { get; }

    private BoqWorkbook(string sourcePath, string readablePath)
    {
        SourcePath = sourcePath;
        _readablePath = readablePath;
        try { _book = new XLWorkbook(readablePath); }
        catch { DeleteTemp(); throw; }
        SheetNames = _book.Worksheets.Where(s => s.Visibility == XLWorksheetVisibility.Visible).Select(s => s.Name).ToList();
        if (SheetNames.Count == 0) SheetNames = _book.Worksheets.Select(s => s.Name).ToList();
    }

    public static BoqWorkbook Open(string path) => new(path, PdvtService.MakeExternalFormulaValuesReadable(path));

    public void Dispose()
    {
        _book.Dispose();
        DeleteTemp();
    }

    private void DeleteTemp()
    {
        if (!string.Equals(_readablePath, SourcePath, StringComparison.OrdinalIgnoreCase) && File.Exists(_readablePath))
            File.Delete(_readablePath);
    }

    // ------------------------------------------------------------------ detection

    /// <summary>Best header row across all sheets; when none is found returns an empty map for the first sheet.</summary>
    public BoqColumnMap Detect()
    {
        BoqColumnMap? best = null;
        var bestScore = 0;
        foreach (var name in SheetNames)
        {
            var sheet = _book.Worksheet(name);
            var used = sheet.RangeUsed();
            if (used == null) continue;
            var first = used.RangeAddress.FirstAddress.RowNumber;
            var last = Math.Min(used.RangeAddress.LastAddress.RowNumber, first + HeaderSearchRows - 1);
            for (var row = first; row <= last; row++)
            {
                var map = DetectAt(name, row);
                if (map.Get(BoqField.NoiDung) == 0) continue;
                var score = map.Columns.Count;
                if (score > bestScore) { best = map; bestScore = score; }
            }
        }
        return best ?? new BoqColumnMap { SheetName = SheetNames.FirstOrDefault() ?? string.Empty, HeaderRow = 1 };
    }

    /// <summary>Maps columns by header name on a given row; the STT column falls back to the column left of "Nội dung".</summary>
    public BoqColumnMap DetectAt(string sheetName, int headerRow)
    {
        var map = new BoqColumnMap { SheetName = sheetName, HeaderRow = headerRow };
        if (!_book.TryGetWorksheet(sheetName, out var sheet) || headerRow < 1) return map;
        foreach (var cell in sheet.Row(headerRow).CellsUsed().OrderBy(c => c.Address.ColumnNumber))
        {
            if (!AliasLookup.TryGetValue(NormalizeHeader(HeaderText(cell)), out var field)) continue;
            var column = cell.Address.ColumnNumber;
            if (map.Get(field) == 0) map.Set(field, column);
            else if (map.Get(field) != column)
            {
                if (!map.Fallbacks.TryGetValue(field, out var more)) map.Fallbacks[field] = more = [];
                if (!more.Contains(column)) more.Add(column);
            }
        }
        var content = map.Get(BoqField.NoiDung);
        if (map.Get(BoqField.Stt) == 0 && content > 1) map.Set(BoqField.Stt, content - 1);
        return map;
    }

    /// <summary>Normalized header texts of a row, used to remember the user's column choices for the same template.</summary>
    public string Signature(string sheetName, int headerRow)
    {
        if (!_book.TryGetWorksheet(sheetName, out var sheet) || headerRow < 1) return string.Empty;
        return string.Join("|", sheet.Row(headerRow).CellsUsed()
            .Select(c => NormalizeHeader(HeaderText(c))).Where(x => x.Length > 0));
    }

    public int LastRow(string sheetName) =>
        _book.TryGetWorksheet(sheetName, out var sheet) ? sheet.RangeUsed()?.RangeAddress.LastAddress.RowNumber ?? 1 : 1;

    /// <summary>Columns that have a header or data, with a short value sample for the mapping dialog.</summary>
    public List<BoqColumnInfo> DescribeColumns(string sheetName, int headerRow)
    {
        var result = new List<BoqColumnInfo>();
        if (!_book.TryGetWorksheet(sheetName, out var sheet) || headerRow < 1) return result;
        var lastColumn = Math.Min(sheet.RangeUsed()?.RangeAddress.LastAddress.ColumnNumber ?? 0, 60);
        var lastRow = Math.Min(LastRow(sheetName), headerRow + 80);
        for (var column = 1; column <= lastColumn; column++)
        {
            var top = sheet.Cell(headerRow, column);
            var header = HeaderText(top);
            // Two-row headers: "Đơn giá" merged over "Vật tư" / "Nhân công".
            var merged = top.MergedRange();
            if (merged != null && merged.ColumnCount() > 1 && headerRow < lastRow)
            {
                var sub = sheet.Cell(headerRow + 1, column).GetString().Trim();
                if (sub.Length > 0) header = $"{header} › {sub}";
            }
            var samples = new List<string>();
            for (var row = headerRow + 1; row <= lastRow && samples.Count < 3; row++)
            {
                var value = Collapse(sheet.Cell(row, column).GetString());
                if (value.Length == 0 || samples.Contains(value)) continue;
                samples.Add(value.Length > 28 ? value[..27] + "…" : value);
            }
            if (header.Length == 0 && samples.Count == 0) continue;
            result.Add(new BoqColumnInfo(column, XLHelper.GetColumnLetterFromNumber(column), Collapse(header), string.Join(" · ", samples)));
        }
        return result;
    }

    // ------------------------------------------------------------------ reading

    public List<PdvtBoqItem> Read(BoqColumnMap map, int maxRows = int.MaxValue)
    {
        var rows = new List<PdvtBoqItem>();
        if (map.Get(BoqField.NoiDung) == 0 || !_book.TryGetWorksheet(map.SheetName, out var sheet)) return rows;
        var lastRow = LastRow(map.SheetName);
        var stack = new List<(string Kind, string Path)>();

        for (var rowNumber = map.HeaderRow + 1; rowNumber <= lastRow && rows.Count < maxRows; rowNumber++)
        {
            string Text(BoqField field) => map.All(field)
                .Select(column => sheet.Cell(rowNumber, column).GetString().Trim())
                .FirstOrDefault(value => value.Length > 0) ?? string.Empty;
            decimal Number(BoqField field) => map.All(field)
                .Select(column => sheet.Cell(rowNumber, column))
                .Where(cell => !cell.IsEmpty())
                .Select(CellNumber)
                .FirstOrDefault();

            var noiDung = Text(BoqField.NoiDung);
            if (noiDung.Length == 0) continue;
            var stt = Text(BoqField.Stt);
            if (IsColumnIndexLabel(stt) && IsColumnIndexLabel(noiDung)) continue;

            var brand = Text(BoqField.NhanHieu);
            var split = PdvtService.SplitBrandOrigin(brand, Text(BoqField.XuatXu));
            var brandOptions = split.Options;
            var approvedOptions = string.IsNullOrWhiteSpace(split.Origin)
                ? brandOptions
                : brandOptions.Count == 0
                    ? [split.Origin]
                    : brandOptions.Select(option => option.Contains(split.Origin, StringComparison.CurrentCultureIgnoreCase)
                        ? option : $"{option} / {split.Origin}").ToList();
            var item = new PdvtBoqItem
            {
                SttBoq = stt,
                NoiDung = noiDung,
                ThongSoKyThuat = Text(BoqField.ThongSo),
                NhanHieuXuatXu = string.Join("; ", approvedOptions),
                NhanHieuTheoHd = brand,
                XuatXuTheoHd = split.Origin,
                Model = Text(BoqField.Model),
                DonViTinh = Text(BoqField.DonVi),
                DonGia = Number(BoqField.DonGia),
                KhoiLuong = Number(BoqField.KhoiLuong),
                NhanHieuOptions = brandOptions
            };

            var isHeading = item.KhoiLuong == 0 && item.DonGia == 0 &&
                            (string.IsNullOrWhiteSpace(item.DonViTinh) || item.DonViTinh == "0" || item.DonViTinh == "-");
            if (isHeading)
            {
                // Group labels without a code ("Ngăn đầu vào") and totals are not BoQ lines.
                if (stt.Length == 0 || noiDung.StartsWith("Tổng", StringComparison.CurrentCultureIgnoreCase)) continue;
                var kind = SttKind(stt);
                var index = stack.FindIndex(level => level.Kind == kind);
                if (index >= 0) stack.RemoveRange(index, stack.Count - index);
                var parent = stack.Count > 0 ? stack[^1].Path : string.Empty;
                var path = parent.Length == 0 ? stt : $"{parent}>{stt}";
                stack.Add((kind, path));
                item.IsHeading = true;
                item.HeadingLevel = stack.Count;
                item.Path = path;
                item.SectionCode = parent;
            }
            else item.SectionCode = stack.Count > 0 ? stack[^1].Path : string.Empty;
            rows.Add(item);
        }
        return rows;
    }

    /// <summary>
    /// Numbering style of a heading code. Each style keeps its depth once seen, so "C → I → I.1 → 1" nests
    /// and a later "II" or "D" closes the deeper levels.
    /// </summary>
    public static string SttKind(string stt)
    {
        var code = stt.Trim().TrimEnd('.', ')').TrimStart('(').Trim();
        if (code.Contains('.')) return "dot" + code.Count(c => c == '.');
        if (Regex.IsMatch(code, "^(X{0,3})(IX|IV|V?I{0,3})$") && code.Length > 0) return "roman";
        if (Regex.IsMatch(code, "^[A-Z]$")) return "letter";
        if (Regex.IsMatch(code, "^[a-zđ]$")) return "lower";
        if (Regex.IsMatch(code, @"^\d+$")) return "number";
        return "other";
    }

    private static bool IsColumnIndexLabel(string value) => Regex.IsMatch(value, @"^\(?\d{1,2}\)?$");

    private static decimal CellNumber(IXLCell cell)
    {
        try
        {
            var value = cell.CachedValue;
            if (value.IsNumber) return (decimal)value.GetNumber();
        }
        catch (Exception) { /* fall back to the displayed text */ }
        return ParseNumber(cell.GetString());
    }

    /// <summary>Parses "1.250,5", "1,250.5", "10,00" and "1.250" (Vietnamese and English separators).</summary>
    public static decimal ParseNumber(string text)
    {
        var s = Regex.Replace(text ?? string.Empty, @"[^\d,.\-]", string.Empty);
        if (s.Length == 0 || s == "-") return 0;
        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
            s = lastComma > lastDot ? s.Replace(".", string.Empty).Replace(',', '.') : s.Replace(",", string.Empty);
        else if (lastComma >= 0)
            s = s.Count(c => c == ',') == 1 && s.Length - lastComma - 1 != 3 ? s.Replace(',', '.') : s.Replace(",", string.Empty);
        else if (lastDot >= 0 && (s.Count(c => c == '.') > 1 || s.Length - lastDot - 1 == 3))
            s = s.Replace(".", string.Empty);
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static string HeaderText(IXLCell cell)
    {
        var merged = cell.MergedRange();
        return (merged != null ? merged.FirstCell() : cell).GetString().Trim();
    }

    private static string Collapse(string value) => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

    /// <summary>Lower-case letters/digits only, without diacritics or bracketed units ("Đơn giá (VNĐ)" → "dongia").</summary>
    public static string NormalizeHeader(string value)
    {
        var withoutUnits = Regex.Replace(value ?? string.Empty, @"\([^)]*\)", string.Empty);
        var withoutMarks = string.Concat(withoutUnits.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
        return string.Concat(withoutMarks.Replace("đ", "d", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit));
    }
}
