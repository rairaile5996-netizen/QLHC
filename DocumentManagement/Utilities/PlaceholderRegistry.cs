using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using DocumentManagement.Models;

namespace DocumentManagement.Utilities;

public static partial class PlaceholderRegistry
{
    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "dd/M/yyyy", "d/MM/yyyy"];

    public sealed record PlaceholderEvaluation(string Value, IReadOnlyList<string> MissingTokens);

    [GeneratedRegex(@"\[(.*?)\]", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();

    public static IReadOnlyList<PlaceholderCatalogItem> GetSystemPlaceholders()
    {
        return
        [
            new() { Key = "{TenDuAn}", Description = "Tên dự án", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{TenDuAn_VietHoa}", Description = "Tên dự án viết hoa", Source = "Calculated", Scope = "CVXD, VTĐV" },
            new() { Key = "{TenCongTrinh}", Description = "Tên công trình", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{HangMuc}", Description = "Tên hạng mục/gói thầu", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{DiaDiem}", Description = "Địa điểm xây dựng", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{SoHD}", Description = "Số hợp đồng", Source = "Document", Scope = "CVXD, VTĐV" },
            new() { Key = "{NgayKy}", Description = "Ngày ký hợp đồng", Source = "CvxdInfo/Document", Scope = "CVXD, VTĐV" },
            new() { Key = "{TenNTC}", Description = "Tên bên giao thầu/chủ đầu tư", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{DaiDienNTC}", Description = "Đại diện bên giao thầu/chủ đầu tư", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{ChucVuNTC}", Description = "Chức vụ bên giao thầu/chủ đầu tư", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{TenNTP}", Description = "Tên bên nhận thầu/nhà thầu", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{DaiDienNTP}", Description = "Đại diện bên nhận thầu/nhà thầu", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{KY_HIEU_NT}", Description = "Ký hiệu công ty bên nhận thầu", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{ChucVuNTP}", Description = "Chức vụ bên nhận thầu/nhà thầu", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{TenTVGS}", Description = "Tên tư vấn giám sát", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{DaiDienTVGS}", Description = "Đại diện tư vấn giám sát", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{KY_HIEU_TVGS}", Description = "Ký hiệu công ty tư vấn giám sát", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{ChucVuTVGS}", Description = "Chức vụ tư vấn giám sát", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{DS_TVGS}", Description = "Danh sách đại diện tư vấn giám sát", Source = "Danh sách đại diện hợp đồng", Scope = "CVXD, VTĐV" },
            new() { Key = "{DS_NTC}", Description = "Danh sách đại diện bên giao thầu/chủ đầu tư", Source = "Danh sách đại diện hợp đồng", Scope = "CVXD, VTĐV" },
            new() { Key = "{DS_NTP}", Description = "Danh sách đại diện bên nhận thầu/nhà thầu", Source = "Danh sách đại diện hợp đồng", Scope = "CVXD, VTĐV" },
            new() { Key = "{KY_HIEU_GT}", Description = "Ký hiệu công ty bên giao thầu", Source = "CvxdInfo", Scope = "CVXD, VTĐV" },
            new() { Key = "{NgayNT}", Description = "Ngày nghiệm thu", Source = "CvxdItem/VtdvSession", Scope = "CVXD, VTĐV" },
            new() { Key = "{BatDau}", Description = "Giờ bắt đầu", Source = "CvxdItem/VtdvSession", Scope = "CVXD, VTĐV" },
            new() { Key = "{KetThuc}", Description = "Giờ kết thúc", Source = "CvxdItem/VtdvSession", Scope = "CVXD, VTĐV" },
            new() { Key = "{TenCauKien}", Description = "Đối tượng/nội dung nghiệm thu", Source = "CvxdItem/VtdvSession", Scope = "CVXD, VTĐV" },
            new() { Key = "{PDVT}", Description = "Số phê duyệt vật tư", Source = "CvxdItem/VtdvSession", Scope = "CVXD, VTĐV" },
            new() { Key = "{4}", Description = "Tên chủ đầu tư/bên giao thầu", Source = "Legacy alias", Scope = "CVXD" },
            new() { Key = "{5}", Description = "Tên tư vấn giám sát", Source = "Legacy alias", Scope = "CVXD" },
            new() { Key = "{6}", Description = "Tên nhà thầu", Source = "Legacy alias", Scope = "CVXD" },
            new() { Key = "{7}", Description = "Số hợp đồng", Source = "Legacy alias", Scope = "CVXD" },
            new() { Key = "{12}", Description = "Đại diện chủ đầu tư/bên giao thầu", Source = "Legacy alias", Scope = "CVXD" },
            new() { Key = "{13}", Description = "Đại diện tư vấn giám sát", Source = "Legacy alias", Scope = "CVXD" },
            new() { Key = "{14}", Description = "Đại diện nhà thầu trong CVXD, số biên bản trong VTĐV", Source = "Legacy alias", Scope = "CVXD, VTĐV" },
            new() { Key = "{18}", Description = "Chức vụ chủ đầu tư/bên giao thầu", Source = "Legacy alias", Scope = "CVXD" },
            new() { Key = "{22}", Description = "Tên tư vấn thiết kế", Source = "Legacy alias", Scope = "CVXD" },
            new() { Key = "{24}", Description = "Số hợp đồng đầu tư / số PDVT", Source = "Legacy alias", Scope = "CVXD, VTĐV" },
            new() { Key = "{25}", Description = "Địa điểm", Source = "Legacy alias", Scope = "CVXD" },
            new() { Key = "{YCNT}", Description = "Số yêu cầu nghiệm thu tự tính", Source = "CVXD calculated", Scope = "CVXD" },
            new() { Key = "{NT}", Description = "Số biên bản nghiệm thu tự tính", Source = "CVXD calculated", Scope = "CVXD" },
            new() { Key = "{TC}", Description = "Nội dung tiêu chuẩn theo TCNT", Source = "CVXD calculated", Scope = "CVXD" },
            new() { Key = "{STT_BB}", Description = "Số thứ tự biên bản theo loại nghiệm thu", Source = "CVXD calculated", Scope = "CVXD" },
            new() { Key = "{STT_checklist}", Description = "Số thứ tự checklist được tính theo thứ tự xuất", Source = "CVXD calculated", Scope = "CVXD" },
            new() { Key = "{SoCL}", Description = "Số checklist được tính khi xuất; trống nếu dòng không có checklist", Source = "CVXD calculated", Scope = "CVXD" },
            new() { Key = "{LoaiBB}", Description = "Tiêu đề loại biên bản theo cột Loại NT", Source = "CVXD calculated", Scope = "CVXD" },
            new() { Key = "{LoaiBb}", Description = "Alias tiêu đề loại biên bản theo cột Loại NT", Source = "CVXD calculated", Scope = "CVXD" },
            new() { Key = "{28}", Description = "Dòng checklist theo Loại NT và STT checklist", Source = "CVXD calculated", Scope = "CVXD" },
            new() { Key = "{SoBB}", Description = "Số thứ tự biên bản VTĐV", Source = "VTĐV calculated", Scope = "VTĐV" },
            new() { Key = "{DoiTuongNT}", Description = "Đối tượng nghiệm thu VTĐV", Source = "VtdvSession", Scope = "VTĐV" },
            new() { Key = "{TenVT}", Description = "Tên vật tư trong bảng VTĐV", Source = "VtdvItem", Scope = "VTĐV" },
            new() { Key = "{DonVi}", Description = "Đơn vị vật tư", Source = "VtdvItem", Scope = "VTĐV" },
            new() { Key = "{SoLuong}", Description = "Số lượng vật tư", Source = "VtdvItem", Scope = "VTĐV" },
            new() { Key = "{Brand}", Description = "Thương hiệu/xuất xứ", Source = "VtdvItem", Scope = "VTĐV" },
            new() { Key = "{Model}", Description = "Mã sản phẩm/CO-CQ", Source = "VtdvItem", Scope = "VTĐV" },
            new() { Key = "[TenProperty]", Description = "Dùng trong giá trị placeholder tùy chỉnh để lấy property tự động", Source = "Resolver", Scope = "CVXD, VTĐV" },
            new() { Key = "[SignDate:dd/MM/yyyy]", Description = "Dùng trong giá trị placeholder tùy chỉnh với định dạng ngày", Source = "Resolver", Scope = "CVXD, VTĐV" }
        ];
    }

    public static bool IsSystemPlaceholder(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var normalized = NormalizeKey(key);
        return GetSystemPlaceholders().Any(p => string.Equals(p.Key, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static Dictionary<string, string> BuildCvxd(
        Document doc,
        CvxdInfo info,
        CvxdGlobalConfig config,
        CvxdItem? item = null)
    {
        var result = BuildBase(doc, info);

        if (item != null)
        {
            AddObjectPlaceholders(result, item);
            Add(result, "NoiDung", item.TenNt?.ToUpperInvariant() ?? "");
            Add(result, "TenCauKien", item.NoiDung);
            Add(result, "NgayNT", item.NgayNt);
            Add(result, "Ngay_NT", item.NgayNt);
            Add(result, "PDVT", item.SoPdvt);
            Add(result, "SoPDVT", item.SoPdvt);
            Add(result, "LoaiBB", GetCvxdLoaiBbTitle(item.LoaiNt));
            Add(result, "LoaiBb", GetCvxdLoaiBbTitle(item.LoaiNt));
            AddDateParts(result, item.NgayNt);
        }

        ApplyFixedPlaceholders(result, config, "CVXD");
        ApplyMappedPlaceholders(result, info, item, "CVXD");
        return result;
    }

    public static Dictionary<string, string> BuildVtdv(
        Document doc,
        CvxdInfo info,
        CvxdGlobalConfig config,
        VtdvSession session)
    {
        var result = BuildBase(doc, info);
        AddObjectPlaceholders(result, session);

        string sttBb = DigitsOnly(session.SoBienBan);
        Add(result, "TenCauKien", session.DoiTuongNghiệmThu);
        Add(result, "DoiTuongNT", session.DoiTuongNghiệmThu);
        Add(result, "SoBB", sttBb);
        Add(result, "14", sttBb);
        Add(result, "NgayNT", session.NgayNghiệmThu);
        Add(result, "Ngay_NT", session.NgayNghiệmThu);
        Add(result, "Gio_NT", session.BatDau);
        Add(result, "PDVT", !string.IsNullOrWhiteSpace(session.SoPheDuyetVatTu) ? session.SoPheDuyetVatTu : info.SoHopDongDauTu);
        Add(result, "SoPDVT", !string.IsNullOrWhiteSpace(session.SoPheDuyetVatTu) ? session.SoPheDuyetVatTu : info.SoHopDongDauTu);
        Add(result, "24", info.SoHopDongDauTu);
        AddDateParts(result, session.NgayNghiệmThu);

        ApplyFixedPlaceholders(result, config, "VTĐV");
        ApplyMappedPlaceholders(result, info, session, "VTĐV");
        return result;
    }

    public static Dictionary<string, string> BuildVtdvItem(VtdvItem item, CvxdInfo? info = null, VtdvSession? session = null, int? rowIndex = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (session != null)
        {
            AddObjectPlaceholders(result, session);
        }
        if (info != null)
        {
            AddObjectPlaceholders(result, info);
            Add(result, "TenDuAn", info.TenDuAn);
            Add(result, "TenDuAn_VietHoa", info.TenDuAn?.ToUpperInvariant());
            Add(result, "DuAn", info.TenDuAn);
            Add(result, "TenCongTrinh", info.TenCongTrinh);
            Add(result, "CongTrinh", info.TenCongTrinh);
            Add(result, "HangMuc", info.TenHangMuc);
            Add(result, "DiaDiem", info.DiaDiem);
            Add(result, "Diadiem", info.DiaDiem);
        }

        AddObjectPlaceholders(result, item);
        Add(result, "STT", !string.IsNullOrWhiteSpace(item.Stt) ? item.Stt : rowIndex?.ToString(CultureInfo.InvariantCulture));
        Add(result, "TenVT", item.DoiTuong);
        Add(result, "DonVi", item.DonVi);
        Add(result, "SoLuong", FormatVtdvQuantity(item.SoLuong));
        Add(result, "Brand", item.ThuongHieu);
        Add(result, "Model", item.MaSanPham);
        Add(result, "ChatLuong", !string.IsNullOrWhiteSpace(item.ChatLuong) ? item.ChatLuong : item.MaSanPham);
        if (info != null)
        {
            ApplyMappedPlaceholders(result, info, item, "VTĐV");
        }
        return result;
    }

    private static string FormatVtdvQuantity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var text = value.Trim();
        var normalized = text.Replace(" ", string.Empty);

        if (normalized.Contains(',') && normalized.Contains('.'))
        {
            normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (normalized.Contains(','))
        {
            normalized = normalized.Replace(',', '.');
        }

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return number.ToString("N2", CultureInfo.GetCultureInfo("vi-VN"));
        }

        return text;
    }

    public static string ResolvePattern(
        string pattern,
        Document doc,
        CvxdInfo info,
        object? context = null)
    {
        return EvaluatePattern(pattern, doc, info, context).Value;
    }

    public static PlaceholderEvaluation EvaluatePattern(
        string pattern,
        Document doc,
        CvxdInfo info,
        object? context = null)
    {
        if (string.IsNullOrEmpty(pattern)) return new PlaceholderEvaluation(string.Empty, []);
        var missingTokens = new List<string>();

        var result = TokenRegex().Replace(pattern, match =>
        {
            var token = match.Groups[1].Value;
            string? format = null;
            if (token.Contains(':'))
            {
                var parts = token.Split(':', 2);
                token = parts[0];
                format = parts[1];
            }

            foreach (var name in token.Split('|'))
            {
                var value = GetPropertyValue(name.Trim(), context, info, doc);
                if (string.IsNullOrEmpty(value)) continue;

                if (!string.IsNullOrWhiteSpace(format) && DateTime.TryParse(value, out var dt))
                    return dt.ToString(format, CultureInfo.InvariantCulture);

                return value;
            }

            missingTokens.Add(match.Value);
            return string.Empty;
        });

        if (result.EndsWith(".ToUpper()", StringComparison.OrdinalIgnoreCase))
        {
            result = result[..^10].ToUpperInvariant();
        }

        return new PlaceholderEvaluation(result, missingTokens);
    }

    private static Dictionary<string, string> BuildBase(Document doc, CvxdInfo info)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddObjectPlaceholders(result, doc);
        AddObjectPlaceholders(result, info);

        Add(result, "TenDuAn", info.TenDuAn);
        Add(result, "TenDuAn_VietHoa", info.TenDuAn?.ToUpperInvariant());
        Add(result, "DuAn", info.TenDuAn);
        Add(result, "TenCongTrinh", info.TenCongTrinh);
        Add(result, "CongTrinh", info.TenCongTrinh);
        Add(result, "HangMuc", info.TenHangMuc);
        Add(result, "DiaDiem", info.DiaDiem);
        Add(result, "Diadiem", info.DiaDiem);
        Add(result, "SoHD", doc.ContractNumber);
        Add(result, "NgayKy", !string.IsNullOrWhiteSpace(info.NgayKyHD) ? info.NgayKyHD : doc.SignDate.ToString("dd/MM/yyyy"));
        Add(result, "ngayky", Get(result, "NgayKy"));
        Add(result, "NGAYKY", Get(result, "NgayKy"));
        Add(result, "TenNTC", info.TenChuDauTu);
        Add(result, "DaiDienNTC", info.DaiDienChuDauTu);
        Add(result, "KY_HIEU_GT", info.KyHieuChuDauTu);
        Add(result, "ChucVuNTC", info.ChucVuCDT);
        Add(result, "ChucvuNTC", info.ChucVuCDT);
        Add(result, "TenNTP", info.TenNhaThau);
        Add(result, "DaiDienNTP", info.DaiDienNhaThau);
        Add(result, "KY_HIEU_NT", info.KyHieuNhaThau);
        Add(result, "ChucVuNTP", info.ChucVuNT);
        Add(result, "ChucvuNTP", info.ChucVuNT);
        Add(result, "TenNhaThauPhu", info.TenNhaThauPhu);
        Add(result, "DaiDienNTP_Phu", info.DaiDienNhaThauPhu);
        Add(result, "TenTVGS", info.TenTuVanGiamSat);
        Add(result, "DaiDienTVGS", info.DaiDienTVGS);
        Add(result, "KY_HIEU_TVGS", info.KyHieuTVGS);
        Add(result, "ChucVuTVGS", info.ChucVuTVGS);
        Add(result, "ChucvuTVGS", info.ChucVuTVGS);
        Add(result, "DS_TVGS", BuildRepresentativeList(info.TvgsRepresentatives, info.DaiDienTVGS, info.ChucVuTVGS));
        Add(result, "DS_NTC", BuildRepresentativeList(info.EmployerRepresentatives, info.DaiDienChuDauTu, info.ChucVuCDT));
        Add(result, "DS_NTP", BuildRepresentativeList(info.ContractorRepresentatives, info.DaiDienNhaThau, info.ChucVuNT));
        Add(result, "CanCuThiCong", info.CanCuThiCong);
        Add(result, "SoHĐDT", info.SoHopDongDauTu);
        Add(result, "NhaThau", info.TenNhaThau);
        Add(result, "4", info.TenChuDauTu);
        Add(result, "5", info.TenTuVanGiamSat);
        Add(result, "6", info.TenNhaThau);
        Add(result, "7", doc.ContractNumber);
        Add(result, "12", info.DaiDienChuDauTu);
        Add(result, "13", info.DaiDienTVGS);
        Add(result, "14", info.DaiDienNhaThau);
        Add(result, "18", info.ChucVuCDT);
        Add(result, "22", info.TenTuVanThietKe);
        Add(result, "24", info.SoHopDongDauTu);
        Add(result, "25", info.DiaDiem);
        return result;
    }

    private static string BuildRepresentativeList(
        IEnumerable<ContractRepresentative>? representatives,
        string? fallbackName,
        string? fallbackPosition)
    {
        var rows = representatives?
            .Where(person => !string.IsNullOrWhiteSpace(person.FullName))
            .Select(person => FormatRepresentative(person.FullName, person.Position))
            .ToList() ?? [];

        if (rows.Count == 0 && !string.IsNullOrWhiteSpace(fallbackName))
        {
            rows.Add(FormatRepresentative(fallbackName, fallbackPosition));
        }

        return string.Join(Environment.NewLine, rows);
    }

    private static string FormatRepresentative(string? fullName, string? position)
    {
        var normalizedName = fullName?.Trim() ?? string.Empty;
        var normalizedPosition = position?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(normalizedPosition)
            ? $"Ông: {normalizedName}"
            : $"Ông: {normalizedName}\tChức vụ: {normalizedPosition}";
    }

    private static void ApplyFixedPlaceholders(Dictionary<string, string> result, CvxdGlobalConfig config, string targetScope)
    {
        if (config.GlobalPlaceholders == null) return;
        foreach (var placeholder in config.GlobalPlaceholders)
        {
            if (string.IsNullOrWhiteSpace(placeholder.Key)) continue;
            if (IsSystemPlaceholder(placeholder.Key)) continue;
            if (!IsScopeMatch(placeholder.Scope, targetScope)) continue;
            result[NormalizeKey(placeholder.Key)] = placeholder.Value ?? string.Empty;
        }
    }

    private static void ApplyMappedPlaceholders(Dictionary<string, string> result, CvxdInfo info, object? context, string targetScope)
    {
        if (info.CustomPlaceholders == null) return;
        foreach (var placeholder in info.CustomPlaceholders)
        {
            if (string.IsNullOrWhiteSpace(placeholder.Key)) continue;
            if (IsSystemPlaceholder(placeholder.Key)) continue;
            if (!IsScopeMatch(placeholder.Scope, targetScope)) continue;
            result[NormalizeKey(placeholder.Key)] = GetContextValue(placeholder.Value, context, info);
        }
    }

    private static bool IsScopeMatch(string? scope, string targetScope)
    {
        if (string.IsNullOrWhiteSpace(scope)) return true;
        return scope.Equals("Cả 2", StringComparison.OrdinalIgnoreCase) ||
               scope.Equals("Ca 2", StringComparison.OrdinalIgnoreCase) ||
               scope.Equals("All", StringComparison.OrdinalIgnoreCase) ||
               scope.Equals(targetScope, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetContextValue(string? sourceName, object? context, CvxdInfo info)
    {
        if (string.IsNullOrWhiteSpace(sourceName)) return string.Empty;
        var name = sourceName.Trim().Trim('[', ']', '{', '}');

        foreach (var source in new[] { context, info })
        {
            if (source == null) continue;

            var customFieldValue = GetCustomFieldValue(source, name);
            if (!string.IsNullOrEmpty(customFieldValue)) return customFieldValue;

            var propValue = GetPropertyValue(name, source);
            if (!string.IsNullOrEmpty(propValue)) return propValue;
        }

        return string.Empty;
    }

    private static string GetCvxdLoaiBbTitle(string? loaiNt)
    {
        var type = loaiNt?.Trim();
        if (string.Equals(type, "CVXD", StringComparison.OrdinalIgnoreCase))
            return "NGHIỆM THU CÔNG VIỆC XÂY DỰNG";
        if (string.Equals(type, "LĐTTB", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "LDTTB", StringComparison.OrdinalIgnoreCase))
            return "NGHIỆM THU LẮP ĐẶT TĨNH THIẾT BỊ";

        return string.Empty;
    }

    private static string? GetCustomFieldValue(object source, string name)
    {
        var prop = source.GetType().GetProperty("CustomFields", BindingFlags.Public | BindingFlags.Instance);
        if (prop?.GetValue(source) is not System.Collections.IEnumerable fields) return null;

        foreach (var field in fields)
        {
            var fieldName = field.GetType().GetProperty("Name")?.GetValue(field)?.ToString();
            if (!string.Equals(fieldName, name, StringComparison.OrdinalIgnoreCase)) continue;
            return field.GetType().GetProperty("Value")?.GetValue(field)?.ToString() ?? string.Empty;
        }

        return null;
    }

    private static void AddObjectPlaceholders(Dictionary<string, string> result, object value)
    {
        foreach (var prop in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            var propValue = prop.GetValue(value);
            if (propValue is null) continue;
            if (propValue is System.Collections.IEnumerable and not string) continue;
            Add(result, prop.Name, FormatValue(propValue));
        }
    }

    private static void AddDateParts(Dictionary<string, string> result, string? dateText)
    {
        if (!DateTime.TryParseExact(dateText, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            Add(result, "1", "");
            Add(result, "2", "");
            Add(result, "3", "");
            Add(result, "15", "");
            Add(result, "16", "");
            Add(result, "17", "");
            return;
        }

        Add(result, "15", date.Day.ToString("D2"));
        Add(result, "16", date.Month.ToString("D2"));
        Add(result, "17", date.Year.ToString(CultureInfo.InvariantCulture));

        var requestDate = date.AddDays(date.DayOfWeek == DayOfWeek.Monday ? -2 : -1);
        Add(result, "1", requestDate.Day.ToString("D2"));
        Add(result, "2", requestDate.Month.ToString("D2"));
        Add(result, "3", requestDate.Year.ToString(CultureInfo.InvariantCulture));
    }

    private static string? GetPropertyValue(string name, params object?[] sources)
    {
        foreach (var source in sources)
        {
            if (source == null) continue;
            var prop = source.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null) continue;
            return FormatValue(prop.GetValue(source));
        }

        return null;
    }

    private static void Add(Dictionary<string, string> result, string key, string? value)
    {
        result[NormalizeKey(key)] = value ?? string.Empty;
    }

    private static string Get(Dictionary<string, string> result, string key)
    {
        return result.TryGetValue(NormalizeKey(key), out var value) ? value : string.Empty;
    }

    private static string NormalizeKey(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}')) return trimmed;
        return "{" + trimmed + "}";
    }

    private static string DigitsOnly(string? value)
    {
        var text = value ?? string.Empty;
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i])) continue;
            start = i;
            break;
        }

        if (start < 0) return string.Empty;

        var end = start;
        while (end < text.Length && char.IsDigit(text[end])) end++;
        return text[start..end];
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            DateTimeOffset date => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }
}
