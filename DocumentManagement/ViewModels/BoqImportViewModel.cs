using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DocumentManagement.Models;
using DocumentManagement.Services;

namespace DocumentManagement.ViewModels;

/// <summary>One BoQ field and the sheet column chosen for it in the import dialog.</summary>
public partial class BoqFieldMapping : ObservableObject
{
    public BoqField Field { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Hint { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    /// <summary>Offers "Nếu trống, lấy cột…" for fields whose values are often split over two columns.</summary>
    public bool AllowsFallback { get; init; }
    [ObservableProperty] private BoqColumnInfo _column = BoqColumnInfo.None;
    [ObservableProperty] private BoqColumnInfo _fallback = BoqColumnInfo.None;
}

/// <summary>
/// Import dialog: shows the detected header row and columns, lets the user re-map any column
/// (e.g. a "Mã hiệu" column that actually holds brands) and previews the parsed rows.
/// </summary>
public partial class BoqImportViewModel : ObservableObject
{
    private const int PreviewRows = 200;
    private readonly BoqWorkbook _workbook;
    private bool _loading;

    public string Title { get; }
    public string FileName { get; }
    public IReadOnlyList<string> SheetNames => _workbook.SheetNames;
    public ObservableCollection<BoqColumnInfo> ColumnOptions { get; } = [];
    public ObservableCollection<BoqFieldMapping> Fields { get; } = [];
    public ObservableCollection<PdvtBoqItem> Preview { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    [ObservableProperty] private string _sheetName;
    [ObservableProperty] private int _headerRow;
    [ObservableProperty] private int _headingCount;
    [ObservableProperty] private int _materialCount;
    [ObservableProperty] private bool _usedSavedLayout;
    [ObservableProperty] private bool _canImport;

    public BoqImportViewModel(BoqWorkbook workbook, BoqColumnMap initial, string title, bool usedSavedLayout = false)
    {
        _workbook = workbook;
        Title = title;
        FileName = System.IO.Path.GetFileName(workbook.SourcePath);
        UsedSavedLayout = usedSavedLayout;
        Fields.Add(new BoqFieldMapping { Field = BoqField.Stt, Label = "STT / mã mục", Hint = "A, I, I.1, 1… dùng để dựng cây đầu mục" });
        Fields.Add(new BoqFieldMapping { Field = BoqField.NoiDung, Label = "Nội dung / tên vật tư", IsRequired = true });
        Fields.Add(new BoqFieldMapping { Field = BoqField.ThongSo, Label = "Thông số kỹ thuật", AllowsFallback = true });
        Fields.Add(new BoqFieldMapping { Field = BoqField.Model, Label = "Model / mã hiệu", AllowsFallback = true });
        Fields.Add(new BoqFieldMapping { Field = BoqField.NhanHieu, Label = "Nhãn hiệu (hoặc cột gộp Nhãn hiệu/Xuất xứ)", AllowsFallback = true });
        Fields.Add(new BoqFieldMapping { Field = BoqField.XuatXu, Label = "Xuất xứ" });
        Fields.Add(new BoqFieldMapping { Field = BoqField.DonVi, Label = "Đơn vị tính" });
        Fields.Add(new BoqFieldMapping { Field = BoqField.KhoiLuong, Label = "Khối lượng" });
        Fields.Add(new BoqFieldMapping { Field = BoqField.DonGia, Label = "Đơn giá" });
        foreach (var field in Fields)
            field.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(BoqFieldMapping.Column) or nameof(BoqFieldMapping.Fallback)) Refresh();
            };

        _sheetName = initial.SheetName;
        _headerRow = Math.Max(1, initial.HeaderRow);
        LoadColumns(initial);
    }

    /// <summary>The confirmed mapping (detected repeated-header fallbacks kept for unchanged fields).</summary>
    public BoqColumnMap Map
    {
        get
        {
            var map = _workbook.DetectAt(SheetName, HeaderRow);
            map.Apply(Fields.ToDictionary(f => f.Field, f => f.Column.Number));
            foreach (var field in Fields.Where(f => f.AllowsFallback && f.Fallback.Number > 0))
                map.SetFallback(field.Field, field.Fallback.Number);
            return map;
        }
    }

    // Changing the sheet or header row re-detects columns by header name.
    partial void OnSheetNameChanged(string value) { if (!_loading) LoadColumns(_workbook.DetectAt(value, HeaderRow)); }
    partial void OnHeaderRowChanged(int value) { if (!_loading && value >= 1) LoadColumns(_workbook.DetectAt(SheetName, value)); }

    /// <summary>Picks the header row automatically for the current sheet.</summary>
    public void AutoDetectSheet()
    {
        var best = _workbook.Detect();
        _loading = true;
        SheetName = best.SheetName;
        HeaderRow = Math.Max(1, best.HeaderRow);
        _loading = false;
        LoadColumns(best);
    }

    private void LoadColumns(BoqColumnMap map)
    {
        _loading = true;
        ColumnOptions.Clear();
        ColumnOptions.Add(BoqColumnInfo.None);
        foreach (var column in _workbook.DescribeColumns(SheetName, HeaderRow)) ColumnOptions.Add(column);
        foreach (var field in Fields)
        {
            var number = map.Get(field.Field);
            field.Column = ColumnOptions.FirstOrDefault(c => c.Number == number) ?? BoqColumnInfo.None;
            var fallback = field.AllowsFallback ? map.GetFallback(field.Field) : 0;
            field.Fallback = ColumnOptions.FirstOrDefault(c => c.Number == fallback) ?? BoqColumnInfo.None;
        }
        _loading = false;
        Refresh();
    }

    private void Refresh()
    {
        if (_loading) return;
        var map = Map;
        var rows = _workbook.Read(map);
        HeadingCount = rows.Count(x => x.IsHeading);
        MaterialCount = rows.Count - HeadingCount;
        Preview.Clear();
        foreach (var row in rows.Take(PreviewRows)) Preview.Add(row);

        Warnings.Clear();
        var materials = rows.Where(x => !x.IsHeading).ToList();
        if (map.Get(BoqField.NoiDung) == 0) Warnings.Add("Chưa chọn cột Nội dung / tên vật tư.");
        else if (materials.Count == 0) Warnings.Add("Không đọc được dòng vật tư nào. Kiểm tra dòng tiêu đề và cột Khối lượng / Đơn vị.");
        if (map.Get(BoqField.NhanHieu) == 0) Warnings.Add("Chưa chọn cột Nhãn hiệu — phiếu PDVT sẽ không có nhãn hiệu theo HĐ.");
        else if (materials.Count >= 5 && materials.Count(x => x.NhanHieuTheoHd.Length == 0) > materials.Count * 0.7)
            Warnings.Add("Cột Nhãn hiệu trống ở hầu hết vật tư — có thể nhãn hiệu nằm ở cột khác (vd. cột \"Mã hiệu\"). Đổi cột, hoặc chọn \"Nếu trống, lấy cột…\".");
        var duplicates = Fields.Where(f => f.Column.Number > 0).GroupBy(f => f.Column.Number).Where(g => g.Count() > 1).ToList();
        foreach (var group in duplicates)
            Warnings.Add($"Cột {group.First().Column.Letter} đang được dùng cho: {string.Join(", ", group.Select(f => f.Label))}.");
        CanImport = materials.Count > 0;
    }
}
