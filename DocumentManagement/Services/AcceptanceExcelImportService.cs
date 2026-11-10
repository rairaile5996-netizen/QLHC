using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using DocumentManagement.Models;

namespace DocumentManagement.Services;

public sealed class AcceptanceExcelImportService
{
    private static readonly HashSet<string> CvxdKnownHeaders =
    [
        "CHON",
        "STT",
        "LOAINT",
        "NOIDUNGNGHIEMTHU",
        "NOIDUNG",
        "NGAYNT",
        "NGAYNGHIEMTHU",
        "BATDAU",
        "KETTHUC",
        "SOCHECKLIST",
        "SOPDVT",
        "SOPHEDUYETVATTU",
        "LOAIBB",
        "TENNT",
        "TCNT"
    ];

    private static readonly HashSet<string> VtdvKnownHeaders =
    [
        "STT",
        "DOITUONG",
        "TENVATTUDOITUONG",
        "SOPHIEUXUAT",
        "SOLUONG",
        "DONVI",
        "THUONGHIEUXUATXU",
        "THUONGHIEUNHANHIEUXUATXU",
        "MASANPHAM",
        "SOCOCQ",
        "CHATLUONG",
        "GHICHU",
        "NGAYNGHIEMTHU",
        "NGAYNT",
        "SOPHEDUYETVATTU",
        "SOPDVT",
        "DOITUONGNGHIEMTHU",
        "DUAN",
        "CONGTRINH",
        "HANGMUC",
        "NHATHAU",
        "BATDAU",
        "KETTHUC",
        "DIADIEM"
    ];

    public AcceptanceExcelImportResult Import(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var result = new AcceptanceExcelImportResult();

        var cvxdSheet = workbook.Worksheets.FirstOrDefault(sheet => string.Equals(NormalizeKey(sheet.Name), "CVXD", StringComparison.Ordinal));
        if (cvxdSheet != null)
        {
            ImportCvxdSheet(cvxdSheet, result);
        }

        foreach (var sheet in workbook.Worksheets.Where(IsVtdvNumberedSheet))
        {
            ImportVtdvSheet(sheet, result);
        }

        return result;
    }

    public AcceptanceExcelImportResult ImportCvxd(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var result = new AcceptanceExcelImportResult();
        var cvxdSheet = workbook.Worksheets.FirstOrDefault(sheet => string.Equals(NormalizeKey(sheet.Name), "CVXD", StringComparison.Ordinal));

        if (cvxdSheet != null)
        {
            ImportCvxdSheet(cvxdSheet, result);
        }

        return result;
    }

    public AcceptanceExcelImportResult ImportVtdv(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var result = new AcceptanceExcelImportResult();

        foreach (var sheet in workbook.Worksheets.Where(IsVtdvNumberedSheet))
        {
            ImportVtdvSheet(sheet, result);
        }

        return result;
    }

    private static void ImportCvxdSheet(IXLWorksheet sheet, AcceptanceExcelImportResult result)
    {
        var headerMap = GetHeaderMap(sheet);
        if (headerMap.Count == 0)
        {
            return;
        }

        var customColumns = headerMap.Values
            .Where(header => !CvxdKnownHeaders.Contains(header.Normalized))
            .Select(header => header.Original)
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in customColumns)
        {
            result.CvxdCustomColumns.Add(name);
        }

        foreach (var row in EnumerateDataRows(sheet, headerMap))
        {
            if (IsRowBlank(row, headerMap.Keys))
            {
                continue;
            }

            var item = new CvxdItem
            {
                IsSelected = GetBooleanCell(row, headerMap, "CHON", fallbackValue: true),
                Stt = GetCellValue(row, headerMap, "STT"),
                LoaiNt = FirstNonEmpty(GetCellValue(row, headerMap, "LOAINT"), "CVXD"),
                NoiDung = FirstNonEmpty(
                    GetCellValue(row, headerMap, "NOIDUNGNGHIEMTHU"),
                    GetCellValue(row, headerMap, "NOIDUNG")),
                NgayNt = FirstNonEmpty(
                    GetCellValue(row, headerMap, "NGAYNT"),
                    GetCellValue(row, headerMap, "NGAYNGHIEMTHU"),
                    DateTime.Now.ToString("dd/MM/yyyy")),
                BatDau = FirstNonEmpty(GetCellValue(row, headerMap, "BATDAU"), "09h00"),
                KetThuc = FirstNonEmpty(GetCellValue(row, headerMap, "KETTHUC"), "10h00"),
                SoChecklist = FirstNonEmpty(GetCellValue(row, headerMap, "SOCHECKLIST"), "1"),
                SoPdvt = FirstNonEmpty(
                    GetCellValue(row, headerMap, "SOPDVT"),
                    GetCellValue(row, headerMap, "SOPHEDUYETVATTU")),
                LoaiBb = GetCellValue(row, headerMap, "LOAIBB"),
                TenNt = GetCellValue(row, headerMap, "TENNT"),
                Tcnt = GetCellValue(row, headerMap, "TCNT"),
                CustomFields = []
            };

            foreach (var customColumn in customColumns)
            {
                item.CustomFields.Add(new CvxdCustomFieldValue
                {
                    Name = customColumn,
                    Value = row.Cell(GetColumnIndexByOriginal(headerMap, customColumn)).GetString().Trim()
                });
            }

            result.CvxdItems.Add(item);
        }
    }

    private static void ImportVtdvSheet(IXLWorksheet sheet, AcceptanceExcelImportResult result)
    {
        var headerMap = GetHeaderMap(sheet);
        if (headerMap.Count == 0)
        {
            return;
        }

        var customColumns = headerMap.Values
            .Where(header => !VtdvKnownHeaders.Contains(header.Normalized))
            .Select(header => header.Original)
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in customColumns)
        {
            result.VtdvCustomColumns.Add(name);
        }

        var session = new VtdvSession
        {
            SoBienBan = sheet.Name.Trim(),
            NgayNghiệmThu = DateTime.Now.ToString("dd/MM/yyyy"),
            SoPheDuyetVatTu = string.Empty,
            DoiTuongNghiệmThu = "Vật tư thiết bị đầu vào",
            BatDau = "08h00",
            KetThuc = "10h00",
            DiaDiem = "Tại công trường",
            Items = [],
            CustomFields = []
        };

        var firstDataRow = EnumerateDataRows(sheet, headerMap).FirstOrDefault();
        if (firstDataRow != null)
        {
            session.NgayNghiệmThu = FirstNonEmpty(
                GetCellValue(firstDataRow, headerMap, "NGAYNGHIEMTHU"),
                GetCellValue(firstDataRow, headerMap, "NGAYNT"),
                session.NgayNghiệmThu);
            session.SoPheDuyetVatTu = FirstNonEmpty(
                GetCellValue(firstDataRow, headerMap, "SOPHEDUYETVATTU"),
                GetCellValue(firstDataRow, headerMap, "SOPDVT"));
            session.DoiTuongNghiệmThu = FirstNonEmpty(
                GetCellValue(firstDataRow, headerMap, "DOITUONGNGHIEMTHU"),
                session.DoiTuongNghiệmThu);
            session.DuAn = GetCellValue(firstDataRow, headerMap, "DUAN");
            session.CongTrinh = GetCellValue(firstDataRow, headerMap, "CONGTRINH");
            session.HangMuc = GetCellValue(firstDataRow, headerMap, "HANGMUC");
            session.NhaThau = GetCellValue(firstDataRow, headerMap, "NHATHAU");
            session.BatDau = FirstNonEmpty(GetCellValue(firstDataRow, headerMap, "BATDAU"), session.BatDau);
            session.KetThuc = FirstNonEmpty(GetCellValue(firstDataRow, headerMap, "KETTHUC"), session.KetThuc);
            session.DiaDiem = FirstNonEmpty(GetCellValue(firstDataRow, headerMap, "DIADIEM"), session.DiaDiem);
        }

        foreach (var row in EnumerateDataRows(sheet, headerMap))
        {
            if (IsRowBlank(row, headerMap.Keys) || !HasAnyItemValue(row, headerMap))
            {
                continue;
            }

            var item = new VtdvItem
            {
                Stt = FirstNonEmpty(GetCellValue(row, headerMap, "STT"), (session.Items.Count + 1).ToString()),
                DoiTuong = FirstNonEmpty(
                    GetCellValue(row, headerMap, "TENVATTUDOITUONG"),
                    GetCellValue(row, headerMap, "DOITUONG")),
                SoPhieuXuat = GetCellValue(row, headerMap, "SOPHIEUXUAT"),
                SoLuong = GetCellValue(row, headerMap, "SOLUONG"),
                DonVi = GetCellValue(row, headerMap, "DONVI"),
                ThuongHieu = FirstNonEmpty(
                    GetCellValue(row, headerMap, "THUONGHIEUXUATXU"),
                    GetCellValue(row, headerMap, "THUONGHIEUNHANHIEUXUATXU")),
                MaSanPham = FirstNonEmpty(
                    GetCellValue(row, headerMap, "MASANPHAM"),
                    GetCellValue(row, headerMap, "SOCOCQ")),
                ChatLuong = GetCellValue(row, headerMap, "CHATLUONG"),
                GhiChu = GetCellValue(row, headerMap, "GHICHU"),
                CustomFields = []
            };

            foreach (var customColumn in customColumns)
            {
                item.CustomFields.Add(new CvxdCustomFieldValue
                {
                    Name = customColumn,
                    Value = row.Cell(GetColumnIndexByOriginal(headerMap, customColumn)).GetString().Trim()
                });
            }

            session.Items.Add(item);
        }

        if (session.Items.Count > 0 || customColumns.Count > 0)
        {
            result.VtdvSessions.Add(session);
        }
    }

    private static bool HasAnyItemValue(IXLRangeRow row, Dictionary<int, HeaderInfo> headerMap)
    {
        return !string.IsNullOrWhiteSpace(GetCellValue(row, headerMap, "TENVATTUDOITUONG")) ||
               !string.IsNullOrWhiteSpace(GetCellValue(row, headerMap, "DOITUONG")) ||
               !string.IsNullOrWhiteSpace(GetCellValue(row, headerMap, "SOLUONG")) ||
               !string.IsNullOrWhiteSpace(GetCellValue(row, headerMap, "DONVI")) ||
               !string.IsNullOrWhiteSpace(GetCellValue(row, headerMap, "THUONGHIEUXUATXU")) ||
               !string.IsNullOrWhiteSpace(GetCellValue(row, headerMap, "THUONGHIEUNHANHIEUXUATXU")) ||
               !string.IsNullOrWhiteSpace(GetCellValue(row, headerMap, "MASANPHAM")) ||
               !string.IsNullOrWhiteSpace(GetCellValue(row, headerMap, "SOCOCQ")) ||
               !string.IsNullOrWhiteSpace(GetCellValue(row, headerMap, "CHATLUONG")) ||
               !string.IsNullOrWhiteSpace(GetCellValue(row, headerMap, "GHICHU"));
    }

    private static bool IsVtdvNumberedSheet(IXLWorksheet sheet)
    {
        var name = sheet.Name.Trim();
        return int.TryParse(name, out _);
    }

    private static Dictionary<int, HeaderInfo> GetHeaderMap(IXLWorksheet sheet)
    {
        var range = sheet.RangeUsed();
        if (range == null)
        {
            return [];
        }

        var headerRow = range.FirstRowUsed();
        if (headerRow == null)
        {
            return [];
        }

        var map = new Dictionary<int, HeaderInfo>();
        foreach (var cell in headerRow.CellsUsed())
        {
            var original = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(original))
            {
                continue;
            }

            map[cell.Address.ColumnNumber] = new HeaderInfo(original, NormalizeKey(original));
        }

        return map;
    }

    private static IEnumerable<IXLRangeRow> EnumerateDataRows(IXLWorksheet sheet, Dictionary<int, HeaderInfo> headerMap)
    {
        var range = sheet.RangeUsed();
        if (range == null)
        {
            yield break;
        }

        var headerRowNumber = range.FirstRowUsed()?.RowNumber() ?? 1;
        foreach (var row in range.RowsUsed().Where(r => r.RowNumber() > headerRowNumber))
        {
            if (IsRowBlank(row, headerMap.Keys))
            {
                continue;
            }

            yield return row;
        }
    }

    private static bool IsRowBlank(IXLRangeRow row, IEnumerable<int> columns)
    {
        foreach (var column in columns)
        {
            if (!string.IsNullOrWhiteSpace(row.Cell(column).GetString()))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetCellValue(IXLRangeRow row, Dictionary<int, HeaderInfo> headerMap, string normalizedHeader)
    {
        var entry = headerMap.FirstOrDefault(x => x.Value.Normalized == normalizedHeader);
        return entry.Equals(default(KeyValuePair<int, HeaderInfo>))
            ? string.Empty
            : row.Cell(entry.Key).GetString().Trim();
    }

    private static int GetColumnIndexByOriginal(Dictionary<int, HeaderInfo> headerMap, string originalHeader)
    {
        return headerMap.First(x => string.Equals(x.Value.Original, originalHeader, StringComparison.OrdinalIgnoreCase)).Key;
    }

    private static bool GetBooleanCell(IXLRangeRow row, Dictionary<int, HeaderInfo> headerMap, string normalizedHeader, bool fallbackValue)
    {
        var value = GetCellValue(row, headerMap, normalizedHeader);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallbackValue;
        }

        var normalized = NormalizeKey(value);
        return normalized is "1" or "X" or "CO" or "TRUE" or "YES" or "CHECKED";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var current = ch is 'đ' or 'Đ' ? 'd' : ch;
            if (char.IsLetterOrDigit(current))
            {
                builder.Append(char.ToUpperInvariant(current));
            }
        }

        return builder.ToString();
    }

    private sealed record HeaderInfo(string Original, string Normalized);
}

public sealed class AcceptanceExcelImportResult
{
    public List<CvxdItem> CvxdItems { get; } = [];
    public List<string> CvxdCustomColumns { get; } = [];
    public List<VtdvSession> VtdvSessions { get; } = [];
    public List<string> VtdvCustomColumns { get; } = [];
    public bool HasCvxdSheet => CvxdItems.Count > 0 || CvxdCustomColumns.Count > 0;
    public bool HasVtdvSheet => VtdvSessions.Count > 0 || VtdvCustomColumns.Count > 0;
    public bool HasAnyData => HasCvxdSheet || HasVtdvSheet;
}
