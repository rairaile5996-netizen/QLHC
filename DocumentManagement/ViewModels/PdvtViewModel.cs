using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentManagement.Models;
using DocumentManagement.Services;
using Microsoft.Win32;

namespace DocumentManagement.ViewModels;

/// <summary>
/// PDVT module: the list of created PDVT sheets is the main screen; the contract BoQ is a
/// secondary view used to look up work items and pick materials into a sheet.
/// </summary>
public partial class PdvtViewModel : ObservableObject
{
    public const string FilterAll = "Tất cả";
    public const string FilterUnassigned = "Chưa lập";
    public const string FilterAssigned = "Đã lập";

    private readonly PdvtService _service = new();
    private readonly MainViewModel _main;
    private readonly Func<CvxdInfo?> _projectInfoProvider;
    private List<PdvtRecord> _records = [];
    private bool _suppressRecordSync;
    private bool _suppressCascade;

    public ObservableCollection<PdvtRecord> Records { get; } = [];
    public ObservableCollection<PdvtBoqItem> BoqItems { get; } = [];
    public ObservableCollection<PdvtBoqItem> FilteredBoqItems { get; } = [];
    public ObservableCollection<PdvtMaterialRow> DraftMaterials { get; } = [];
    public string[] BoqFilterOptions { get; } = [FilterAll, FilterUnassigned, FilterAssigned];

    [ObservableProperty] private PdvtRecord? _selectedRecord;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _boqSearchText = string.Empty;
    [ObservableProperty] private string _boqFilter = FilterAll;
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>True when the right pane shows the BoQ instead of the sheet editor.</summary>
    [ObservableProperty] private bool _isBoqMode;
    /// <summary>True when the BoQ was opened from a sheet to pick materials into it.</summary>
    [ObservableProperty] private bool _isPickingForDraft;
    /// <summary>True while a sheet (saved or new) is open in the editor.</summary>
    [ObservableProperty] private bool _hasDraft;
    [ObservableProperty] private bool _isEditingExisting;

    [ObservableProperty] private string _draftNumber = string.Empty;
    [ObservableProperty] private DateTime _draftDate = DateTime.Today;
    [ObservableProperty] private string _draftCategory = string.Empty;
    [ObservableProperty] private string _draftSupplier = string.Empty;

    public int MaterialBoqCount => BoqItems.Count(x => !x.IsHeading);
    public int SelectedBoqCount => BoqItems.Count(x => !x.IsHeading && x.IsSelected);
    public int AssignedBoqCount => BoqItems.Count(x => !x.IsHeading && !string.IsNullOrEmpty(x.PdvtNumbers));
    public int UnassignedBoqCount => MaterialBoqCount - AssignedBoqCount;
    public bool HasBoq => MaterialBoqCount > 0;
    public bool HasRecords => _records.Count > 0;
    public string DraftTitle => IsEditingExisting ? DraftNumber : $"{DraftNumber} (chưa lưu)";

    public PdvtViewModel(MainViewModel main, Func<CvxdInfo?>? projectInfoProvider = null)
    {
        _main = main;
        _projectInfoProvider = projectInfoProvider ?? (() => null);
        BoqItems.CollectionChanged += (_, _) => UpdateCounts();
        Load();
    }

    public void RefreshContext() => Load();

    partial void OnSearchTextChanged(string value) => RefreshRecords();
    partial void OnBoqSearchTextChanged(string value) => RefreshFilteredBoq();
    partial void OnBoqFilterChanged(string value) => RefreshFilteredBoq();
    partial void OnDraftNumberChanged(string value) => OnPropertyChanged(nameof(DraftTitle));
    partial void OnIsEditingExistingChanged(bool value) => OnPropertyChanged(nameof(DraftTitle));

    partial void OnSelectedRecordChanged(PdvtRecord? value)
    {
        if (_suppressRecordSync || value == null) return;
        LoadRecordIntoDraft(value);
        IsBoqMode = false;
        IsPickingForDraft = false;
    }

    // ------------------------------------------------------------------ loading

    private void Load()
    {
        _records = _main.SelectedDocument == null ? [] : _service.Load(_main.SelectedDocument.Id);
        BoqItems.Clear();
        if (_main.SelectedDocument != null)
            foreach (var item in _service.LoadBoq(_main.SelectedDocument.Id)) AddBoqItem(item);
        UpdateLinks();
        RefreshRecords();
    }

    private void RefreshRecords()
    {
        var selectedId = SelectedRecord?.Id;
        Records.Clear();
        var query = _records.Where(x => string.IsNullOrWhiteSpace(SearchText)
            || x.Number.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
            || x.HạngMục.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
            || x.Materials.Any(m => m.NoiDung.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)));
        foreach (var item in query.OrderByDescending(x => x.Number, StringComparer.CurrentCultureIgnoreCase)) Records.Add(item);
        OnPropertyChanged(nameof(HasRecords));

        // Keep an unsaved new sheet open while the user searches.
        if (HasDraft && !IsEditingExisting) return;

        var next = Records.FirstOrDefault(x => x.Id == selectedId) ?? Records.FirstOrDefault();
        if (next != null)
        {
            _suppressRecordSync = true;
            SelectedRecord = next;
            _suppressRecordSync = false;
            LoadRecordIntoDraft(next);
        }
        else
        {
            ClearDraft();
        }
    }

    private void LoadRecordIntoDraft(PdvtRecord record)
    {
        IsEditingExisting = true;
        HasDraft = true;
        DraftNumber = record.Number;
        DraftDate = record.Date;
        DraftCategory = record.HạngMục;
        DraftSupplier = record.NhaCungCap;
        DraftMaterials.Clear();
        foreach (var row in record.Materials)
        {
            var cloned = Clone(row);
            if (string.IsNullOrWhiteSpace(cloned.NhanHieuPheDuyet))
                cloned.NhanHieuPheDuyet = !string.IsNullOrWhiteSpace(cloned.NhanHieuTheoHd)
                    ? cloned.NhanHieuTheoHd
                    : cloned.NhanHieuOptions.FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cloned.ThongSoKyThuat)) cloned.ThongSoKyThuat = cloned.ThongSoTheoHd;
            if (string.IsNullOrWhiteSpace(cloned.XuatXuDeTrinh)) cloned.XuatXuDeTrinh = cloned.XuatXuTheoHd;
            DraftMaterials.Add(cloned);
        }
        UpdateDraftOrder();
    }

    private void ClearDraft()
    {
        _suppressRecordSync = true;
        SelectedRecord = null;
        _suppressRecordSync = false;
        HasDraft = false;
        IsEditingExisting = false;
        DraftMaterials.Clear();
        DraftNumber = string.Empty;
        DraftCategory = string.Empty;
        DraftSupplier = string.Empty;
    }

    private void StartBlankDraft()
    {
        _suppressRecordSync = true;
        SelectedRecord = null;
        _suppressRecordSync = false;
        HasDraft = true;
        IsEditingExisting = false;
        DraftMaterials.Clear();
        DraftNumber = $"PDVT {PdvtService.NextNumber(_records):00}";
        DraftDate = DateTime.Today;
        DraftCategory = string.Empty;
        DraftSupplier = string.Empty;
    }

    // ------------------------------------------------------------------ BoQ

    private void AddBoqItem(PdvtBoqItem item)
    {
        item.IsSelected = false;
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(PdvtBoqItem.IsSelected)) return;
            if (item.IsHeading && !_suppressCascade) CascadeHeadingSelection(item);
            UpdateCounts();
        };
        BoqItems.Add(item);
    }

    /// <summary>Ticking a section heading (I.1, I.2...) ticks every material inside that section.</summary>
    private void CascadeHeadingSelection(PdvtBoqItem heading)
    {
        _suppressCascade = true;
        var code = heading.SttBoq;
        foreach (var child in BoqItems.Where(x => x != heading && !string.IsNullOrEmpty(code) &&
                     (string.Equals(x.SectionCode, code, StringComparison.OrdinalIgnoreCase) ||
                      x.SectionCode.StartsWith(code + ".", StringComparison.OrdinalIgnoreCase) ||
                      (x.IsHeading && x.SttBoq.StartsWith(code + ".", StringComparison.OrdinalIgnoreCase)))))
            child.IsSelected = heading.IsSelected;
        _suppressCascade = false;
    }

    private void UpdateCounts()
    {
        OnPropertyChanged(nameof(SelectedBoqCount));
        OnPropertyChanged(nameof(MaterialBoqCount));
        OnPropertyChanged(nameof(AssignedBoqCount));
        OnPropertyChanged(nameof(UnassignedBoqCount));
        OnPropertyChanged(nameof(HasBoq));
    }

    /// <summary>Recomputes which PDVT sheet(s) each BoQ material already belongs to.</summary>
    private void UpdateLinks()
    {
        foreach (var item in BoqItems)
        {
            if (item.IsHeading) { item.PdvtNumbers = string.Empty; continue; }
            var numbers = _records
                .Where(record => record.Materials.Any(material =>
                    material.BoqItemId == item.Id ||
                    (string.IsNullOrEmpty(material.BoqItemId) &&
                     string.Equals(material.NoiDung, item.NoiDung, StringComparison.CurrentCultureIgnoreCase) &&
                     string.Equals(material.ThongSoKyThuat, item.ThongSoKyThuat, StringComparison.CurrentCultureIgnoreCase))))
                .Select(record => record.Number)
                .Distinct()
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            item.PdvtNumbers = string.Join(", ", numbers);
        }
        RefreshFilteredBoq();
        UpdateCounts();
    }

    private void RefreshFilteredBoq()
    {
        FilteredBoqItems.Clear();
        var keyword = BoqSearchText?.Trim() ?? string.Empty;
        var hasKeyword = !string.IsNullOrWhiteSpace(keyword);
        var showHeadings = !hasKeyword && BoqFilter == FilterAll;
        foreach (var item in BoqItems)
        {
            if (item.IsHeading)
            {
                if (showHeadings) FilteredBoqItems.Add(item);
                continue;
            }
            var assigned = !string.IsNullOrEmpty(item.PdvtNumbers);
            if (BoqFilter == FilterUnassigned && assigned) continue;
            if (BoqFilter == FilterAssigned && !assigned) continue;
            if (hasKeyword &&
                !item.NoiDung.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) &&
                !item.ThongSoKyThuat.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) &&
                !item.NhanHieuXuatXu.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) &&
                !item.PdvtNumbers.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
                continue;
            FilteredBoqItems.Add(item);
        }
    }

    private void ClearBoqSelection()
    {
        _suppressCascade = true;
        foreach (var item in BoqItems) item.IsSelected = false;
        _suppressCascade = false;
        UpdateCounts();
    }

    // ------------------------------------------------------------------ commands

    /// <summary>"Lập phiếu": opens a new blank sheet in the editor.</summary>
    [RelayCommand]
    private void New()
    {
        // From the BoQ view with ticked rows → the new sheet starts with those rows.
        var carrySelection = IsBoqMode && !IsPickingForDraft && SelectedBoqCount > 0;
        StartBlankDraft();
        if (carrySelection) AddSelectedBoq();
        else ClearBoqSelection();
        IsBoqMode = false;
        IsPickingForDraft = false;
        StatusMessage = string.Empty;
    }

    /// <summary>"Xem BoQ": look up the contract BoQ without picking into a sheet.</summary>
    [RelayCommand]
    private void OpenBoq()
    {
        ClearBoqSelection();
        IsPickingForDraft = false;
        IsBoqMode = true;
    }

    /// <summary>"Thêm vật tư từ BoQ": open the BoQ in picking mode for the current sheet.</summary>
    [RelayCommand]
    private void PickFromBoq()
    {
        if (!HasDraft) StartBlankDraft();
        ClearBoqSelection();
        BoqFilter = FilterUnassigned;
        IsPickingForDraft = true;
        IsBoqMode = true;
    }

    [RelayCommand]
    private void BackToSheet()
    {
        ClearBoqSelection();
        IsBoqMode = false;
        IsPickingForDraft = false;
    }

    [RelayCommand]
    private void OpenRecordFromBoq(string? numbers)
    {
        if (string.IsNullOrWhiteSpace(numbers)) return;
        var first = numbers.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var record = Records.FirstOrDefault(x => string.Equals(x.Number, first, StringComparison.CurrentCultureIgnoreCase));
        if (record != null) SelectedRecord = record;
    }

    [RelayCommand]
    private void ImportBoq()
    {
        if (_main.SelectedDocument == null) { StatusMessage = "Chọn hợp đồng trước khi nạp BoQ."; return; }
        var dialog = new OpenFileDialog { Filter = "Excel BoQ (.xlsx, .xlsm)|*.xlsx;*.xlsm" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var imported = PdvtService.ReadBoq(dialog.FileName);
            if (!imported.Any(item => !item.IsHeading)) { StatusMessage = "File BoQ không có dòng vật tư nào."; return; }
            var usedIds = new HashSet<string>();
            foreach (var item in imported)
            {
                var prior = BoqItems.FirstOrDefault(old =>
                    !usedIds.Contains(old.Id) &&
                    string.Equals(old.NoiDung, item.NoiDung, StringComparison.CurrentCultureIgnoreCase) &&
                    string.Equals(old.ThongSoKyThuat, item.ThongSoKyThuat, StringComparison.CurrentCultureIgnoreCase));
                if (prior != null) item.Id = prior.Id;
                else
                {
                    var linked = _records.SelectMany(record => record.Materials).FirstOrDefault(material =>
                        !usedIds.Contains(material.BoqItemId) &&
                        string.Equals(material.NoiDung, item.NoiDung, StringComparison.CurrentCultureIgnoreCase) &&
                        string.Equals(material.ThongSoKyThuat, item.ThongSoKyThuat, StringComparison.CurrentCultureIgnoreCase));
                    if (linked != null && !string.IsNullOrWhiteSpace(linked.BoqItemId)) item.Id = linked.BoqItemId;
                }
                usedIds.Add(item.Id);
            }
            BoqItems.Clear();
            foreach (var item in imported) AddBoqItem(item);
            _service.SaveBoq(_main.SelectedDocument.Id, BoqItems);
            UpdateLinks();
            StatusMessage = $"Đã nạp {MaterialBoqCount} vật tư từ {System.IO.Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex) { StatusMessage = $"Không đọc được BoQ: {ex.Message}"; }
    }

    /// <summary>Adds the ticked BoQ materials into the open sheet, defaulting specs/brand/origin to the contract.</summary>
    [RelayCommand]
    private void AddSelectedBoq()
    {
        if (SelectedBoqCount == 0) { StatusMessage = "Chưa tích vật tư nào."; return; }
        if (!HasDraft) StartBlankDraft();
        var added = 0;
        foreach (var item in BoqItems.Where(x => !x.IsHeading && x.IsSelected).ToList())
        {
            if (DraftMaterials.Any(x => x.BoqItemId == item.Id)) continue;
            var thongSo = !string.IsNullOrWhiteSpace(item.ThongSoKyThuat) ? item.ThongSoKyThuat : item.Model;
            var brandHd = string.IsNullOrWhiteSpace(item.NhanHieuTheoHd) ? item.NhanHieuXuatXu : item.NhanHieuTheoHd;
            var brandSubmitted = !string.IsNullOrWhiteSpace(brandHd) ? brandHd : item.NhanHieuOptions.FirstOrDefault() ?? string.Empty;
            DraftMaterials.Add(new PdvtMaterialRow
            {
                BoqItemId = item.Id,
                NoiDung = item.NoiDung,
                NoiDungTheoHd = item.NoiDung,
                ThongSoKyThuat = thongSo,
                ThongSoTheoHd = thongSo,
                NhanHieuTheoHd = brandHd,
                NhanHieuPheDuyet = brandSubmitted,
                XuatXuTheoHd = item.XuatXuTheoHd,
                XuatXuDeTrinh = item.XuatXuTheoHd,
                Model = item.Model,
                ModelTheoHd = item.Model,
                DonViTinh = item.DonViTinh,
                NhanHieuOptions = item.NhanHieuOptions.ToList(),
                DonGia = item.DonGia,
                KhoiLuong = item.KhoiLuong
            });
            added++;
        }
        var boqOrder = BoqItems.Select((item, index) => (item.Id, index)).ToDictionary(x => x.Id, x => x.index);
        var ordered = DraftMaterials.OrderBy(row => boqOrder.TryGetValue(row.BoqItemId, out var i) ? i : int.MaxValue).ToList();
        DraftMaterials.Clear();
        foreach (var row in ordered) DraftMaterials.Add(row);
        UpdateDraftOrder();
        ClearBoqSelection();
        IsBoqMode = false;
        IsPickingForDraft = false;
        StatusMessage = added == 0 ? "Các vật tư đã chọn đều có sẵn trong phiếu." : $"Đã thêm {added} vật tư vào {DraftNumber}. Bấm Lưu để ghi lại.";
    }

    [RelayCommand]
    private void AddExtra()
    {
        if (!HasDraft) StartBlankDraft();
        DraftMaterials.Add(new PdvtMaterialRow { Source = "Phát sinh" });
        UpdateDraftOrder();
    }

    [RelayCommand]
    private void RemoveMaterial(PdvtMaterialRow? row)
    {
        if (row == null) return;
        DraftMaterials.Remove(row);
        UpdateDraftOrder();
    }

    private void UpdateDraftOrder()
    {
        for (var index = 0; index < DraftMaterials.Count; index++) DraftMaterials[index].Stt = index + 1;
    }

    [RelayCommand]
    private void Save() => SaveDraft(openExcel: false);

    /// <summary>"Xuất Excel": saves the sheet, regenerates the Excel file and opens it.</summary>
    [RelayCommand]
    private void ExportExcel() => SaveDraft(openExcel: true);

    private void SaveDraft(bool openExcel)
    {
        if (_main.SelectedDocument == null) { StatusMessage = "Chọn hợp đồng trước khi lập PDVT."; return; }
        if (!HasDraft) return;
        if (DraftMaterials.Count == 0) { StatusMessage = "Phiếu chưa có vật tư nào."; return; }
        var number = string.IsNullOrWhiteSpace(DraftNumber) ? $"PDVT {PdvtService.NextNumber(_records):00}" : DraftNumber.Trim();
        var current = IsEditingExisting ? SelectedRecord : null;
        if (_records.Any(x => x != current && string.Equals(x.Number, number, StringComparison.CurrentCultureIgnoreCase)))
        {
            StatusMessage = $"Số phiếu {number} đã tồn tại. Đổi số phiếu khác.";
            return;
        }

        var record = current ?? new PdvtRecord { DocumentId = _main.SelectedDocument.Id };
        record.Number = number;
        record.Date = DraftDate;
        record.HạngMục = DraftCategory;
        record.NhaCungCap = DraftSupplier;
        record.Materials = DraftMaterials.Select(Clone).ToList();
        if (current == null) _records.Add(record);
        _service.Save(_main.SelectedDocument.Id, _records);

        _suppressRecordSync = true;
        IsEditingExisting = true;
        UpdateLinks();
        RefreshRecords();
        SelectedRecord = record;
        _suppressRecordSync = false;
        LoadRecordIntoDraft(record);

        try
        {
            record.ExcelPath = _service.ExportExcel(record, _main.SelectedDocument, _projectInfoProvider());
            _service.Save(_main.SelectedDocument.Id, _records);
            if (openExcel)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(record.ExcelPath) { UseShellExecute = true });
                StatusMessage = $"Đã lưu và mở {System.IO.Path.GetFileName(record.ExcelPath)}.";
            }
            else StatusMessage = $"Đã lưu {record.Number}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Đã lưu {record.Number}, nhưng chưa tạo được Excel: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DeleteRecord()
    {
        if (_main.SelectedDocument == null || !HasDraft) return;
        if (!IsEditingExisting || SelectedRecord == null)
        {
            ClearDraft();
            RefreshRecords();
            return;
        }
        var target = SelectedRecord;
        var confirm = MessageBox.Show($"Xóa phiếu {target.Number}? File Excel đã xuất (nếu có) vẫn giữ nguyên.",
            "Xóa phiếu PDVT", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;
        _records.Remove(target);
        _service.Save(_main.SelectedDocument.Id, _records);
        ClearDraft();
        UpdateLinks();
        RefreshRecords();
        StatusMessage = $"Đã xóa {target.Number}.";
    }

    private static PdvtMaterialRow Clone(PdvtMaterialRow row) => new()
    {
        Id = row.Id, Source = row.Source, BoqItemId = row.BoqItemId, NoiDung = row.NoiDung,
        NoiDungTheoHd = row.NoiDungTheoHd, ThongSoKyThuat = row.ThongSoKyThuat, ThongSoTheoHd = row.ThongSoTheoHd,
        ModelTheoHd = row.ModelTheoHd, NhanHieuTheoHd = row.NhanHieuTheoHd,
        XuatXuTheoHd = row.XuatXuTheoHd, XuatXuDeTrinh = row.XuatXuDeTrinh,
        NhanHieuPheDuyet = row.NhanHieuPheDuyet, Model = row.Model, DonViTinh = row.DonViTinh,
        DonGia = row.DonGia, KhoiLuong = row.KhoiLuong, Status = row.Status,
        ApprovalRound = row.ApprovalRound, ApprovedDate = row.ApprovedDate, Note = row.Note,
        NhanHieuOptions = row.NhanHieuOptions.ToList()
    };
}
