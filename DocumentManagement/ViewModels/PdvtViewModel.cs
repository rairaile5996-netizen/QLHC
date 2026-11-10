using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentManagement.Models;
using DocumentManagement.Services;
using Microsoft.Win32;

namespace DocumentManagement.ViewModels;

/// <summary>One step of the sheet lifecycle stepper. State: done, cur, todo. Only the current (or final) step shows its label.</summary>
public sealed record PdvtStep(int Number, string Label, string State, bool IsLast, bool ShowLabel);

/// <summary>
/// PDVT module. Two views share one header: "Phiếu PDVT" (sheet list + editor, BoQ picker as a drawer)
/// and "Tổng hợp BoQ" (every BoQ line with its approval state across rounds).
/// </summary>
public partial class PdvtViewModel : ObservableObject
{
    public const string RecordFilterAll = "all";
    public const string RecordFilterDraft = "draft";
    public const string RecordFilterSubmitted = "sub";
    public const string RecordFilterRework = "fix";
    public const string RecordFilterApproved = "ok";

    public const string BoqFilterUnassigned = "unassigned";
    public const string BoqFilterAll = "all";
    public const string BoqFilterAssigned = "assigned";

    public const string SummaryFilterAll = "all";
    public const string SummaryFilterUnassigned = "unassigned";
    public const string SummaryFilterPending = "pending";
    public const string SummaryFilterRework = "rework";
    public const string SummaryFilterApproved = "approved";

    public const string AllCategories = "Tất cả hạng mục";

    private readonly PdvtService _service = new();
    private readonly MainViewModel _main;
    private readonly Func<CvxdInfo?> _projectInfoProvider;
    private List<PdvtRecord> _records = [];
    private PdvtRecord? _editingRecord;
    private bool _suppressRecordSync;
    private bool _suppressCascade;
    private bool _loadingDraft;
    private bool _bulkBoq;
    private bool _settingAllChecked;
    private List<(PdvtMaterialRow Row, PdvtApprovalStatus Status, DateTime? Date)> _undo = [];
    private DispatcherTimer? _toastTimer;

    /// <summary>Confirmation hook (message, title, buttons). Tests replace it to avoid modal dialogs.</summary>
    public Func<string, string, MessageBoxButton, MessageBoxResult> Ask { get; set; } =
        (message, title, buttons) => MessageBox.Show(message, title, buttons, MessageBoxImage.Question);

    public ObservableCollection<PdvtRecord> Records { get; } = [];
    public ObservableCollection<PdvtBoqItem> BoqItems { get; } = [];
    public ObservableCollection<PdvtBoqItem> DrawerBoqItems { get; } = [];
    public ObservableCollection<PdvtBoqItem> SummaryItems { get; } = [];
    public ObservableCollection<PdvtMaterialRow> DraftMaterials { get; } = [];
    public ObservableCollection<PdvtStep> Steps { get; } = [];
    public ObservableCollection<string> SummaryCategories { get; } = [AllCategories];
    public ICollectionView DraftMaterialsView { get; }

    public IReadOnlyList<PdvtApprovalStatusOption> ApprovalOptions { get; } =
    [
        new(PdvtApprovalStatus.DaTrinh, "Chưa có kết quả"),
        new(PdvtApprovalStatus.DaDuyet, "Đã duyệt"),
        new(PdvtApprovalStatus.YeuCauSua, "Yêu cầu sửa"),
        new(PdvtApprovalStatus.KhongDuyet, "Không duyệt")
    ];

    // ------------------------------------------------------------------ view state
    [ObservableProperty] private bool _isSummaryView;
    [ObservableProperty] private bool _isBoqDrawerOpen;

    // sheet list
    [ObservableProperty] private PdvtRecord? _selectedRecord;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _recordFilter = RecordFilterAll;
    [ObservableProperty] private int _countAllRecords;
    [ObservableProperty] private int _countDraftRecords;
    [ObservableProperty] private int _countSubmittedRecords;
    [ObservableProperty] private int _countReworkRecords;
    [ObservableProperty] private int _countApprovedRecords;

    // editor
    [ObservableProperty] private bool _hasDraft;
    [ObservableProperty] private bool _isEditingExisting;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _draftNumber = string.Empty;
    [ObservableProperty] private DateTime _draftDate = DateTime.Today;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(IsLocked), nameof(IsEditable))] private DateTime? _draftSubmittedDate;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(RoundLabel))] private int _draftVersion = 1;
    [ObservableProperty] private string _draftCategory = string.Empty;
    [ObservableProperty] private string _draftSupplier = string.Empty;
    [ObservableProperty] private string _draftStatusLabel = "Nháp";
    [ObservableProperty] private string _draftStatusKind = "draft";
    [ObservableProperty] private int _draftTotal;
    [ObservableProperty] private int _draftApproved;
    [ObservableProperty] private int _draftRework;
    [ObservableProperty] private int _draftRejected;
    [ObservableProperty] private int _draftPending;
    [ObservableProperty] private int _diffCount;
    [ObservableProperty] private int _checkedCount;
    [ObservableProperty] private bool _isComplete;
    [ObservableProperty] private bool _canResubmit;
    [ObservableProperty] private string _resubmitLabel = string.Empty;
    [ObservableProperty] private bool _showOnlyDiff;
    [ObservableProperty] private bool _showOnlyPending;

    // BoQ drawer
    [ObservableProperty] private string _boqSearchText = string.Empty;
    [ObservableProperty] private string _boqFilter = BoqFilterUnassigned;
    [ObservableProperty] private int _selectedBoqCount;

    // summary
    [ObservableProperty] private string _summaryFilter = SummaryFilterAll;
    [ObservableProperty] private string _summarySearchText = string.Empty;
    [ObservableProperty] private string _summaryCategory = AllCategories;
    [ObservableProperty] private bool _collapseHeadings;
    [ObservableProperty] private PdvtBoqItem? _selectedSummaryItem;
    [ObservableProperty] private int _kpiUnassigned;
    [ObservableProperty] private int _kpiPending;
    [ObservableProperty] private int _kpiRework;
    [ObservableProperty] private int _kpiApproved;
    [ObservableProperty] private int _kpiTotal;

    // toast
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isToastVisible;
    [ObservableProperty] private bool _isToastError;
    [ObservableProperty] private bool _canUndo;

    public bool IsLocked => DraftSubmittedDate != null;
    public bool IsEditable => !IsLocked;
    public string RoundLabel => $"Lần trình {DraftVersion}";
    public bool HasRecords => _records.Count > 0;
    public string ContractLabel => _main.SelectedDocument is { } document && !string.IsNullOrWhiteSpace(document.ContractNumber)
        ? $"HĐ {document.ContractNumber}"
        : "Chưa chọn hợp đồng";
    public bool HasBoq => KpiTotal > 0;
    public int UnassignedBoqCount => KpiUnassigned;
    public int ApprovedPercent => KpiTotal == 0 ? 0 : (int)Math.Round(100.0 * KpiApproved / KpiTotal);

    /// <summary>Tri-state header checkbox of the result table.</summary>
    public bool? AllChecked
    {
        get
        {
            var visible = VisibleDraftRows().ToList();
            if (visible.Count == 0 || visible.All(x => !x.IsChecked)) return false;
            return visible.All(x => x.IsChecked) ? true : null;
        }
        set
        {
            _settingAllChecked = true;
            var target = value != false;
            foreach (var row in VisibleDraftRows()) row.IsChecked = target;
            _settingAllChecked = false;
            UpdateCheckedCount();
        }
    }

    public PdvtViewModel(MainViewModel main, Func<CvxdInfo?>? projectInfoProvider = null)
    {
        _main = main;
        _projectInfoProvider = projectInfoProvider ?? (() => null);
        DraftMaterialsView = CollectionViewSource.GetDefaultView(DraftMaterials);
        DraftMaterialsView.Filter = FilterDraftRow;
        BoqItems.CollectionChanged += OnBoqItemsChanged;
        DraftMaterials.CollectionChanged += OnDraftMaterialsChanged;
        Load();
    }

    public void RefreshContext() => Load();

    // ------------------------------------------------------------------ property hooks

    partial void OnSearchTextChanged(string value) => RefreshRecords();
    partial void OnRecordFilterChanged(string value) => RefreshRecords();
    partial void OnBoqSearchTextChanged(string value) => RefreshDrawer();
    partial void OnBoqFilterChanged(string value) => RefreshDrawer();
    partial void OnSummaryFilterChanged(string value) => RefreshSummary();
    partial void OnSummarySearchTextChanged(string value) => RefreshSummary();
    partial void OnSummaryCategoryChanged(string value) => RefreshSummary();
    partial void OnCollapseHeadingsChanged(bool value) => RefreshSummary();
    partial void OnShowOnlyDiffChanged(bool value) => RefreshDraftView();
    partial void OnShowOnlyPendingChanged(bool value) => RefreshDraftView();
    partial void OnDraftNumberChanged(string value) => MarkDirty();
    partial void OnDraftDateChanged(DateTime value) => MarkDirty();
    partial void OnDraftCategoryChanged(string value) => MarkDirty();
    partial void OnDraftSupplierChanged(string value) => MarkDirty();
    partial void OnDraftSubmittedDateChanged(DateTime? value) => MarkDirty();
    partial void OnKpiTotalChanged(int value) { OnPropertyChanged(nameof(HasBoq)); OnPropertyChanged(nameof(ApprovedPercent)); }
    partial void OnKpiApprovedChanged(int value) => OnPropertyChanged(nameof(ApprovedPercent));
    partial void OnKpiUnassignedChanged(int value) => OnPropertyChanged(nameof(UnassignedBoqCount));

    partial void OnIsSummaryViewChanged(bool value)
    {
        IsBoqDrawerOpen = false;
        ClearBoqSelection();
        if (value) RefreshSummary();
    }

    partial void OnIsBoqDrawerOpenChanged(bool value)
    {
        ClearBoqSelection();
        if (value) RefreshDrawer();
    }

    partial void OnSelectedRecordChanged(PdvtRecord? oldValue, PdvtRecord? newValue)
    {
        if (_suppressRecordSync || newValue == null || ReferenceEquals(newValue, _editingRecord)) return;
        if (!NeedsSaveConfirmation)
        {
            OpenRecord(newValue);
            return;
        }
        // The list is still raising SelectionChanged; ask afterwards and restore the selection meanwhile.
        RunLater(() =>
        {
            SetSelectedSilently(_editingRecord);
            if (ConfirmLeave()) OpenRecord(newValue);
        });
    }

    // ------------------------------------------------------------------ loading

    private void Load()
    {
        _records = _main.SelectedDocument == null ? [] : _service.Load(_main.SelectedDocument.Id);
        _bulkBoq = true;
        BoqItems.Clear();
        if (_main.SelectedDocument != null)
            foreach (var item in _service.LoadBoq(_main.SelectedDocument.Id)) BoqItems.Add(item);
        _bulkBoq = false;
        Recompute();
        ClearBoqSelection();
        ClearDraft();
        RefreshRecords();
        if (Records.FirstOrDefault() is { } first) OpenRecord(first);
        OnPropertyChanged(nameof(HasRecords));
        OnPropertyChanged(nameof(ContractLabel));
    }

    private void OnBoqItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (PdvtBoqItem item in e.NewItems) AttachBoqItem(item);
        if (!_bulkBoq) Recompute();
    }

    private void AttachBoqItem(PdvtBoqItem item)
    {
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(PdvtBoqItem.IsSelected)) return;
            if (item.IsHeading && !_suppressCascade) CascadeHeadingSelection(item);
            UpdateSelectedBoqCount();
        };
    }

    /// <summary>Recomputes BoQ progress, KPIs, record counts and every derived list.</summary>
    private void Recompute()
    {
        PdvtProgress.Apply(BoqItems, _records);
        var materials = BoqItems.Where(x => !x.IsHeading).ToList();
        KpiTotal = materials.Count;
        KpiUnassigned = materials.Count(x => x.Progress == PdvtBoqProgress.ChuaLap);
        KpiPending = materials.Count(x => x.Progress == PdvtBoqProgress.ChoKetQua);
        KpiRework = materials.Count(x => x.Progress == PdvtBoqProgress.CanTrinhLai);
        KpiApproved = materials.Count(x => x.Progress == PdvtBoqProgress.DaDuyet);

        var keepCategory = SummaryCategory;
        SummaryCategories.Clear();
        SummaryCategories.Add(AllCategories);
        foreach (var heading in BoqItems.Where(x => x.IsTopHeading)) SummaryCategories.Add(CategoryLabel(heading));
        SummaryCategory = SummaryCategories.Contains(keepCategory) ? keepCategory : AllCategories;

        RefreshSummary();
        RefreshDrawer();
    }

    private static string CategoryLabel(PdvtBoqItem heading) =>
        string.IsNullOrWhiteSpace(heading.SttBoq) ? heading.NoiDung : $"{heading.SttBoq} · {heading.NoiDung}";

    // ------------------------------------------------------------------ sheet list

    private void RefreshRecords()
    {
        foreach (var record in _records)
            record.NextRoundNumber = _records.FirstOrDefault(x => x.PreviousRecordId == record.Id)?.Number ?? string.Empty;
        CountAllRecords = _records.Count;
        CountDraftRecords = _records.Count(x => RecordCategory(x) == RecordFilterDraft);
        CountSubmittedRecords = _records.Count(x => RecordCategory(x) == RecordFilterSubmitted);
        CountReworkRecords = _records.Count(x => RecordCategory(x) == RecordFilterRework);
        CountApprovedRecords = _records.Count(x => RecordCategory(x) == RecordFilterApproved);

        var keyword = SearchText.Trim();
        var query = _records.Where(x =>
            (RecordFilter == RecordFilterAll || RecordCategory(x) == RecordFilter) &&
            (keyword.Length == 0 ||
             x.Number.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
             x.HạngMục.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
             x.NhaCungCap.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
             x.Materials.Any(m => m.NoiDung.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))));

        _suppressRecordSync = true;
        Records.Clear();
        foreach (var item in query.OrderByDescending(x => x.Number, NaturalComparer.Instance)) Records.Add(item);
        SelectedRecord = _editingRecord != null && Records.Contains(_editingRecord) ? _editingRecord : null;
        _suppressRecordSync = false;
        OnPropertyChanged(nameof(HasRecords));
    }

    /// <summary>Sheets whose rejected rows already went into a later round only appear under "Tất cả".</summary>
    private static string RecordCategory(PdvtRecord record) => record.DerivedStatus switch
    {
        PdvtApprovalStatus.ChuaTrinh => RecordFilterDraft,
        PdvtApprovalStatus.DaTrinh => RecordFilterSubmitted,
        PdvtApprovalStatus.DaDuyet => RecordFilterApproved,
        _ => string.IsNullOrEmpty(record.NextRoundNumber) ? RecordFilterRework : "resubmitted"
    };

    private void SetSelectedSilently(PdvtRecord? record)
    {
        _suppressRecordSync = true;
        SelectedRecord = record != null && Records.Contains(record) ? record : null;
        _suppressRecordSync = false;
    }

    private void OpenRecord(PdvtRecord record)
    {
        SetSelectedSilently(record);
        LoadRecordIntoDraft(record);
        IsBoqDrawerOpen = false;
        IsSummaryView = false;
    }

    // ------------------------------------------------------------------ editor

    private bool NeedsSaveConfirmation => HasDraft && IsDirty && (IsEditingExisting || DraftMaterials.Count > 0);

    /// <summary>Returns false when the user cancels leaving a sheet with unsaved changes.</summary>
    private bool ConfirmLeave()
    {
        if (!NeedsSaveConfirmation) return true;
        var answer = Ask($"{DraftNumber} có thay đổi chưa lưu.\nLưu lại trước khi rời phiếu?", "Thay đổi chưa lưu", MessageBoxButton.YesNoCancel);
        return answer switch
        {
            MessageBoxResult.Yes => SaveDraft(openExcel: false),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    private void LoadRecordIntoDraft(PdvtRecord record)
    {
        _loadingDraft = true;
        _editingRecord = record;
        IsEditingExisting = true;
        HasDraft = true;
        DraftNumber = record.Number;
        DraftDate = record.Date;
        DraftSubmittedDate = record.SubmittedDate;
        DraftVersion = record.Version;
        DraftCategory = record.HạngMục;
        DraftSupplier = record.NhaCungCap;
        ClearDraftMaterials();
        foreach (var row in record.Materials)
        {
            var cloned = Clone(row);
            if (string.IsNullOrWhiteSpace(cloned.NhanHieuPheDuyet))
                cloned.NhanHieuPheDuyet = !string.IsNullOrWhiteSpace(cloned.NhanHieuTheoHd)
                    ? cloned.NhanHieuTheoHd
                    : cloned.NhanHieuOptions.FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cloned.ThongSoKyThuat)) cloned.ThongSoKyThuat = cloned.ThongSoTheoHd;
            if (string.IsNullOrWhiteSpace(cloned.XuatXuDeTrinh)) cloned.XuatXuDeTrinh = cloned.XuatXuTheoHd;
            if (record.IsSubmitted && cloned.Status == PdvtApprovalStatus.ChuaTrinh) cloned.Status = PdvtApprovalStatus.DaTrinh;
            DraftMaterials.Add(cloned);
        }
        ShowOnlyDiff = false;
        ShowOnlyPending = false;
        _undo = [];
        CanUndo = false;
        _loadingDraft = false;
        IsDirty = false;
        UpdateDraftOrder();
        UpdateDraftStats();
    }

    private void ClearDraft()
    {
        _loadingDraft = true;
        _editingRecord = null;
        SetSelectedSilently(null);
        HasDraft = false;
        IsEditingExisting = false;
        ClearDraftMaterials();
        DraftNumber = string.Empty;
        DraftCategory = string.Empty;
        DraftSupplier = string.Empty;
        DraftSubmittedDate = null;
        DraftVersion = 1;
        _loadingDraft = false;
        IsDirty = false;
        UpdateDraftStats();
    }

    private void StartBlankDraft()
    {
        ClearDraft();
        _loadingDraft = true;
        HasDraft = true;
        DraftNumber = $"PDVT {PdvtService.NextNumber(_records):00}";
        DraftDate = DateTime.Today;
        _loadingDraft = false;
        IsDirty = false;
        UpdateDraftStats();
    }

    private void MarkDirty()
    {
        if (!_loadingDraft && HasDraft) IsDirty = true;
    }

    /// <summary>Clear() raises Reset without OldItems, so detach row handlers first.</summary>
    private void ClearDraftMaterials()
    {
        foreach (var row in DraftMaterials) row.PropertyChanged -= OnDraftRowChanged;
        DraftMaterials.Clear();
    }

    private void OnDraftMaterialsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (PdvtMaterialRow row in e.OldItems) row.PropertyChanged -= OnDraftRowChanged;
        if (e.NewItems != null)
            foreach (PdvtMaterialRow row in e.NewItems) row.PropertyChanged += OnDraftRowChanged;
        MarkDirty();
        if (!_loadingDraft) UpdateDraftStats();
    }

    private void OnDraftRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PdvtMaterialRow row) return;
        switch (e.PropertyName)
        {
            case nameof(PdvtMaterialRow.IsChecked):
                if (!_settingAllChecked) UpdateCheckedCount();
                return;
            case nameof(PdvtMaterialRow.Stt):
            case nameof(PdvtMaterialRow.IsBrandDiff):
            case nameof(PdvtMaterialRow.IsOriginDiff):
            case nameof(PdvtMaterialRow.IsDiff):
            case nameof(PdvtMaterialRow.StatusLabel):
            case nameof(PdvtMaterialRow.HasResult):
                return;
            case nameof(PdvtMaterialRow.Status):
                row.ApprovedDate = row.Status == PdvtApprovalStatus.DaDuyet ? row.ApprovedDate ?? DateTime.Today : null;
                break;
        }
        MarkDirty();
        if (!_loadingDraft) UpdateDraftStats();
    }

    private void UpdateDraftStats()
    {
        var rows = DraftMaterials.ToList();
        var snapshot = new PdvtRecord { SubmittedDate = DraftSubmittedDate, Materials = rows };
        DraftTotal = rows.Count;
        DraftApproved = snapshot.ApprovedCount;
        DraftRework = snapshot.ReworkCount;
        DraftRejected = snapshot.RejectedCount;
        DraftPending = IsLocked ? snapshot.PendingCount : rows.Count;
        DiffCount = rows.Count(x => x.IsDiff);
        DraftStatusLabel = snapshot.StatusLabel;
        DraftStatusKind = snapshot.StatusKind;
        IsComplete = IsLocked && rows.Count > 0 && DraftPending == 0;
        CanResubmit = IsComplete && DraftRework + DraftRejected > 0;
        var next = _editingRecord == null ? null : _records.FirstOrDefault(x => x.PreviousRecordId == _editingRecord.Id);
        ResubmitLabel = next != null ? $"Mở {next.Number}" : $"Trình lại (lần {DraftVersion + 1})";

        var step = !IsLocked ? 1 : IsComplete ? 5 : 3;
        string[] labels = ["Nháp", "Đã trình", "Ghi nhận kết quả", "Hoàn tất"];
        var steps = labels.Select((label, i) => new PdvtStep(i + 1, label,
            step > i + 1 ? "done" : step == i + 1 ? "cur" : "todo", i == labels.Length - 1,
            step == i + 1 || (step > labels.Length && i == labels.Length - 1))).ToList();
        if (!Steps.SequenceEqual(steps))
        {
            Steps.Clear();
            foreach (var s in steps) Steps.Add(s);
        }
        UpdateCheckedCount();
    }

    private void UpdateCheckedCount()
    {
        CheckedCount = DraftMaterials.Count(x => x.IsChecked);
        OnPropertyChanged(nameof(AllChecked));
        OnPropertyChanged(nameof(ShowBulkBar));
        OnPropertyChanged(nameof(ShowBulkHint));
    }

    public bool ShowBulkBar => IsLocked && CheckedCount > 0;
    public bool ShowBulkHint => IsLocked && CheckedCount == 0;

    private void UpdateDraftOrder()
    {
        for (var index = 0; index < DraftMaterials.Count; index++) DraftMaterials[index].Stt = index + 1;
    }

    private bool FilterDraftRow(object obj) =>
        obj is PdvtMaterialRow row &&
        (!ShowOnlyDiff || row.IsDiff) &&
        (!ShowOnlyPending || !row.HasResult);

    private void RefreshDraftView()
    {
        DraftMaterialsView.Refresh();
        OnPropertyChanged(nameof(AllChecked));
    }

    private IEnumerable<PdvtMaterialRow> VisibleDraftRows() => DraftMaterialsView.Cast<PdvtMaterialRow>();

    // ------------------------------------------------------------------ BoQ drawer + selection

    private void CascadeHeadingSelection(PdvtBoqItem heading)
    {
        var visible = IsBoqDrawerOpen ? DrawerBoqItems : SummaryItems;
        _suppressCascade = true;
        foreach (var child in visible.Where(x => PdvtProgress.IsInSection(x, heading) && !x.IsInDraft))
            child.IsSelected = heading.IsSelected;
        _suppressCascade = false;
    }

    private void UpdateSelectedBoqCount() =>
        SelectedBoqCount = BoqItems.Count(x => !x.IsHeading && x.IsSelected && !(IsBoqDrawerOpen && x.IsInDraft));

    private void ClearBoqSelection()
    {
        _suppressCascade = true;
        foreach (var item in BoqItems) item.IsSelected = false;
        _suppressCascade = false;
        UpdateSelectedBoqCount();
    }

    private void RefreshDrawer()
    {
        var inDraft = DraftMaterials.Select(x => x.BoqItemId).Where(x => x.Length > 0).ToHashSet();
        foreach (var item in BoqItems) item.IsInDraft = !item.IsHeading && inDraft.Contains(item.Id);

        var keyword = BoqSearchText.Trim();
        bool Visible(PdvtBoqItem item) =>
            (BoqFilter == BoqFilterAll ||
             (BoqFilter == BoqFilterUnassigned ? item.Progress == PdvtBoqProgress.ChuaLap || item.IsInDraft
                                               : item.Progress != PdvtBoqProgress.ChuaLap)) &&
            MatchesKeyword(item, keyword);
        Fill(DrawerBoqItems, BuildHierarchy(BoqItems, Visible, headingsOnly: false));
        UpdateSelectedBoqCount();
    }

    private void RefreshSummary()
    {
        var keyword = SummarySearchText.Trim();
        var section = SummaryCategory == AllCategories
            ? null
            : BoqItems.FirstOrDefault(x => x.IsTopHeading && CategoryLabel(x) == SummaryCategory);
        bool Visible(PdvtBoqItem item) =>
            (section == null || PdvtProgress.IsInSection(item, section)) &&
            SummaryFilter switch
            {
                SummaryFilterUnassigned => item.Progress == PdvtBoqProgress.ChuaLap,
                SummaryFilterPending => item.Progress == PdvtBoqProgress.ChoKetQua,
                SummaryFilterRework => item.Progress == PdvtBoqProgress.CanTrinhLai,
                SummaryFilterApproved => item.Progress == PdvtBoqProgress.DaDuyet,
                _ => true
            } &&
            MatchesKeyword(item, keyword);
        var keep = SelectedSummaryItem;
        Fill(SummaryItems, BuildHierarchy(BoqItems, Visible, CollapseHeadings, section));
        SelectedSummaryItem = keep != null && SummaryItems.Contains(keep) ? keep : null;
    }

    private static bool MatchesKeyword(PdvtBoqItem item, string keyword) =>
        keyword.Length == 0 ||
        item.NoiDung.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
        item.ThongSoKyThuat.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
        item.NhanHieuXuatXu.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
        item.NhanHieuTheoHd.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
        item.ApprovedBrand.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
        item.PdvtNumbers.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ||
        item.SttBoq.Contains(keyword, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Materials passing the filter, each preceded by the headings of the sections that contain visible materials.</summary>
    private static List<PdvtBoqItem> BuildHierarchy(IList<PdvtBoqItem> source, Func<PdvtBoqItem, bool> visible, bool headingsOnly, PdvtBoqItem? section = null)
    {
        var shown = source.Where(x => !x.IsHeading && visible(x)).ToHashSet();
        var result = new List<PdvtBoqItem>();
        foreach (var item in source)
        {
            if (item.IsHeading)
            {
                var inScope = section == null || ReferenceEquals(item, section) || PdvtProgress.IsInSection(item, section);
                if (inScope && shown.Any(x => PdvtProgress.IsInSection(x, item))) result.Add(item);
            }
            else if (!headingsOnly && shown.Contains(item)) result.Add(item);
        }
        // Flat BoQs without headings still need their materials when collapsed.
        return headingsOnly && result.Count == 0 ? source.Where(shown.Contains).ToList() : result;
    }

    private static void Fill(ObservableCollection<PdvtBoqItem> target, List<PdvtBoqItem> items)
    {
        if (target.SequenceEqual(items)) return;
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    private int AddBoqItemsToDraft(IEnumerable<PdvtBoqItem> items)
    {
        var added = 0;
        foreach (var item in items.Where(x => !x.IsHeading))
        {
            if (DraftMaterials.Any(x => x.BoqItemId == item.Id)) continue;
            var thongSo = !string.IsNullOrWhiteSpace(item.ThongSoKyThuat) ? item.ThongSoKyThuat : item.Model;
            var brandHd = string.IsNullOrWhiteSpace(item.NhanHieuTheoHd) ? item.NhanHieuXuatXu : item.NhanHieuTheoHd;
            var brandSubmitted = !string.IsNullOrWhiteSpace(brandHd) ? item.NhanHieuOptions.FirstOrDefault() ?? brandHd : string.Empty;
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
        if (!ordered.SequenceEqual(DraftMaterials))
        {
            ClearDraftMaterials();
            foreach (var row in ordered) DraftMaterials.Add(row);
        }
        UpdateDraftOrder();
        return added;
    }

    // ------------------------------------------------------------------ commands: navigation

    [RelayCommand]
    private void ShowRecords() => IsSummaryView = false;

    [RelayCommand]
    private void ShowSummary() => IsSummaryView = true;

    [RelayCommand]
    private void New()
    {
        if (!ConfirmLeave()) return;
        StartBlankDraft();
        IsSummaryView = false;
        IsBoqDrawerOpen = false;
    }

    /// <summary>Creates a sheet from the BoQ lines ticked in the summary.</summary>
    [RelayCommand]
    private void NewFromSelected()
    {
        var selected = BoqItems.Where(x => !x.IsHeading && x.IsSelected).ToList();
        if (selected.Count == 0) { ShowToast("Chưa tích vật tư nào trong bảng.", isError: true); return; }
        if (!ConfirmLeave()) return;
        StartBlankDraft();
        var added = AddBoqItemsToDraft(selected);
        IsSummaryView = false;
        IsBoqDrawerOpen = false;
        ClearBoqSelection();
        ShowToast($"Đã tạo {DraftNumber} với {added} vật tư. Bấm Lưu để ghi lại.");
    }

    [RelayCommand]
    private void CloseEditor()
    {
        if (!ConfirmLeave()) return;
        ClearDraft();
    }

    [RelayCommand]
    private void OpenRecordById(string? id)
    {
        var record = _records.FirstOrDefault(x => x.Id == id);
        if (record == null) return;
        if (ReferenceEquals(record, _editingRecord)) { IsSummaryView = false; IsBoqDrawerOpen = false; return; }
        if (!ConfirmLeave()) return;
        if (!Records.Contains(record)) { SearchText = string.Empty; RecordFilter = RecordFilterAll; }
        OpenRecord(record);
    }

    // ------------------------------------------------------------------ commands: BoQ

    [RelayCommand]
    private void PickFromBoq()
    {
        if (IsLocked) return;
        if (!HasDraft) StartBlankDraft();
        BoqSearchText = string.Empty;
        BoqFilter = BoqFilterUnassigned;
        IsBoqDrawerOpen = true;
    }

    [RelayCommand]
    private void CloseDrawer() => IsBoqDrawerOpen = false;

    [RelayCommand]
    private void ClearBoqSelectionAction() => ClearBoqSelection();

    [RelayCommand]
    private void AddSelectedBoq()
    {
        var selected = BoqItems.Where(x => !x.IsHeading && x.IsSelected && !x.IsInDraft).ToList();
        if (selected.Count == 0) { ShowToast("Chưa tích vật tư nào.", isError: true); return; }
        if (!HasDraft) StartBlankDraft();
        var added = AddBoqItemsToDraft(selected);
        IsBoqDrawerOpen = false;
        ShowToast($"Đã thêm {added} vật tư vào {DraftNumber}. Bấm Lưu để ghi lại.");
    }

    [RelayCommand]
    private void ImportBoq()
    {
        if (_main.SelectedDocument == null) { ShowToast("Chọn hợp đồng trước khi nạp BoQ.", isError: true); return; }
        var dialog = new OpenFileDialog { Filter = "Excel BoQ (.xlsx, .xlsm)|*.xlsx;*.xlsm" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var imported = PdvtService.ReadBoq(dialog.FileName);
            if (!imported.Any(item => !item.IsHeading)) { ShowToast("File BoQ không có dòng vật tư nào.", isError: true); return; }
            // Keep ids of lines that already existed so saved sheets stay linked.
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
            _bulkBoq = true;
            BoqItems.Clear();
            foreach (var item in imported) BoqItems.Add(item);
            _bulkBoq = false;
            _service.SaveBoq(_main.SelectedDocument.Id, BoqItems);
            Recompute();
            ShowToast($"Đã nạp {KpiTotal} vật tư từ {System.IO.Path.GetFileName(dialog.FileName)}.");
        }
        catch (Exception ex)
        {
            _bulkBoq = false;
            ShowToast($"Không đọc được BoQ: {ex.Message}", isError: true);
        }
    }

    // ------------------------------------------------------------------ commands: editing

    [RelayCommand]
    private void AddExtra()
    {
        if (IsLocked) return;
        if (!HasDraft) StartBlankDraft();
        DraftMaterials.Add(new PdvtMaterialRow { Source = "Phát sinh" });
        UpdateDraftOrder();
    }

    [RelayCommand]
    private void RemoveMaterial(PdvtMaterialRow? row)
    {
        if (row == null || IsLocked) return;
        DraftMaterials.Remove(row);
        UpdateDraftOrder();
    }

    [RelayCommand]
    private void Save() => SaveDraft(openExcel: false);

    /// <summary>Saves, regenerates the Excel sheet and opens it.</summary>
    [RelayCommand]
    private void ExportExcel() => SaveDraft(openExcel: true);

    [RelayCommand]
    private void Submit()
    {
        if (!HasDraft || IsLocked) return;
        if (DraftMaterials.Count == 0) { ShowToast("Phiếu chưa có vật tư nào.", isError: true); return; }
        var confirm = Ask($"Trình {DraftNumber} với {DraftMaterials.Count} vật tư?\nSau khi trình, nội dung phiếu sẽ được khóa; bạn chỉ ghi nhận kết quả duyệt.",
            "Trình duyệt PDVT", MessageBoxButton.OKCancel);
        if (confirm != MessageBoxResult.OK) return;
        DraftSubmittedDate = DateTime.Today;
        foreach (var row in DraftMaterials.Where(x => x.Status == PdvtApprovalStatus.ChuaTrinh)) row.Status = PdvtApprovalStatus.DaTrinh;
        if (SaveDraft(openExcel: false, exportExcel: true, quiet: true))
            ShowToast($"Đã trình {DraftNumber}. Ghi nhận kết quả khi có phản hồi của TVGS.");
    }

    /// <summary>Returns a submitted sheet to draft, e.g. when it was marked as submitted by mistake.</summary>
    [RelayCommand]
    private void Unsubmit()
    {
        if (!IsLocked) return;
        var withResults = DraftMaterials.Count(x => x.HasResult);
        var message = withResults > 0
            ? $"Đưa {DraftNumber} về Nháp? Kết quả đã ghi của {withResults} vật tư sẽ bị xóa."
            : $"Đưa {DraftNumber} về Nháp để sửa nội dung?";
        if (Ask(message, "Hủy trình", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        DraftSubmittedDate = null;
        foreach (var row in DraftMaterials) { row.Status = PdvtApprovalStatus.ChuaTrinh; row.ApprovedDate = null; }
        SaveDraft(openExcel: false, quiet: true);
        ShowToast($"{DraftNumber} đã về Nháp.");
    }

    /// <summary>Creates the next round with every row that was not approved.</summary>
    [RelayCommand]
    private void Resubmit()
    {
        if (_main.SelectedDocument == null || _editingRecord == null || !IsLocked) return;
        if (IsDirty && !SaveDraft(openExcel: false, quiet: true)) return;
        var source = _editingRecord;
        var next = _records.FirstOrDefault(x => x.PreviousRecordId == source.Id);
        if (next != null)
        {
            OpenRecord(next);
            ShowToast($"Vật tư cần trình lại đã nằm trong {next.Number}.");
            return;
        }
        var rows = source.Materials.Where(x => x.Status is PdvtApprovalStatus.YeuCauSua or PdvtApprovalStatus.KhongDuyet).ToList();
        if (rows.Count == 0) { ShowToast("Phiếu không có vật tư nào cần trình lại."); return; }

        var baseNumber = Regex.Replace(source.Number, @"_v\d+$", string.Empty, RegexOptions.IgnoreCase);
        var version = source.Version + 1;
        while (_records.Any(x => string.Equals(x.Number, $"{baseNumber}_v{version}", StringComparison.CurrentCultureIgnoreCase))) version++;
        var record = new PdvtRecord
        {
            DocumentId = _main.SelectedDocument.Id,
            Number = $"{baseNumber}_v{version}",
            Version = version,
            PreviousRecordId = source.Id,
            Date = DateTime.Today,
            HạngMục = source.HạngMục,
            NhaCungCap = source.NhaCungCap,
            Materials = rows.Select(row =>
            {
                var copy = Clone(row);
                copy.Id = Guid.NewGuid().ToString("N");
                copy.Status = PdvtApprovalStatus.ChuaTrinh;
                copy.ApprovedDate = null;
                copy.ReviewNote = string.Empty;
                // Keep the supervisor's feedback visible while the contractor revises the row.
                if (!string.IsNullOrWhiteSpace(row.ReviewNote) && string.IsNullOrWhiteSpace(copy.Note)) copy.Note = $"TVGS: {row.ReviewNote}";
                return copy;
            }).ToList()
        };
        _records.Add(record);
        _service.Save(_main.SelectedDocument.Id, _records);
        Recompute();
        RefreshRecords();
        OpenRecord(record);
        ShowToast($"Đã tạo {record.Number} (lần {version}) với {rows.Count} vật tư cần trình lại.");
    }

    [RelayCommand]
    private void DeleteRecord()
    {
        if (_main.SelectedDocument == null || !HasDraft) return;
        if (_editingRecord == null)
        {
            ClearDraft();
            return;
        }
        var target = _editingRecord;
        var confirm = Ask($"Xóa phiếu {target.Number}? File Excel đã xuất (nếu có) vẫn giữ nguyên.", "Xóa phiếu PDVT", MessageBoxButton.YesNo);
        if (confirm != MessageBoxResult.Yes) return;
        _records.Remove(target);
        foreach (var child in _records.Where(x => x.PreviousRecordId == target.Id)) child.PreviousRecordId = string.Empty;
        _service.Save(_main.SelectedDocument.Id, _records);
        ClearDraft();
        Recompute();
        RefreshRecords();
        ShowToast($"Đã xóa {target.Number}.");
    }

    [RelayCommand]
    private void OpenExcelFile()
    {
        var path = _editingRecord?.ExcelPath;
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) { ExportExcel(); return; }
        OpenPath(path);
    }

    // ------------------------------------------------------------------ commands: results

    [RelayCommand]
    private void BulkApprove() => SetCheckedStatus(PdvtApprovalStatus.DaDuyet);

    [RelayCommand]
    private void BulkRework() => SetCheckedStatus(PdvtApprovalStatus.YeuCauSua);

    [RelayCommand]
    private void BulkReject() => SetCheckedStatus(PdvtApprovalStatus.KhongDuyet);

    [RelayCommand]
    private void BulkClear() => SetCheckedStatus(PdvtApprovalStatus.DaTrinh);

    private void SetCheckedStatus(PdvtApprovalStatus status)
    {
        if (!IsLocked) return;
        var rows = DraftMaterials.Where(x => x.IsChecked).ToList();
        if (rows.Count == 0) { ShowToast("Tích chọn vật tư trước khi ghi kết quả.", isError: true); return; }
        _undo = rows.Select(x => (x, x.Status, x.ApprovedDate)).ToList();
        foreach (var row in rows) { row.Status = status; row.IsChecked = false; }
        RefreshDraftView();
        var label = ApprovalOptions.First(x => x.Value == status).DisplayText.ToLowerInvariant();
        ShowToast($"Đã đánh dấu {rows.Count} vật tư: {label}. Bấm Lưu kết quả để ghi lại.", canUndo: true);
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undo.Count == 0) return;
        foreach (var (row, status, date) in _undo) { row.Status = status; row.ApprovedDate = date; }
        _undo = [];
        RefreshDraftView();
        ShowToast("Đã hoàn tác.");
    }

    // ------------------------------------------------------------------ commands: summary

    [RelayCommand]
    private void ExportSummary()
    {
        if (_main.SelectedDocument == null) { ShowToast("Chọn hợp đồng trước.", isError: true); return; }
        if (!HasBoq) { ShowToast("Chưa có dữ liệu BoQ để xuất.", isError: true); return; }
        try
        {
            var path = _service.ExportSummary(BoqItems, _main.SelectedDocument);
            OpenPath(path);
            ShowToast($"Đã xuất {System.IO.Path.GetFileName(path)}.");
        }
        catch (Exception ex) { ShowToast($"Chưa xuất được bảng tổng hợp: {ex.Message}", isError: true); }
    }

    [RelayCommand]
    private void CreateFromItem(PdvtBoqItem? item)
    {
        if (item == null || item.IsHeading) return;
        ClearBoqSelection();
        item.IsSelected = true;
        NewFromSelected();
    }

    [RelayCommand]
    private void ResubmitItem(PdvtBoqItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.LatestRecordId)) return;
        OpenRecordById(item.LatestRecordId);
        if (_editingRecord?.Id == item.LatestRecordId) Resubmit();
    }

    // ------------------------------------------------------------------ save

    private bool SaveDraft(bool openExcel, bool? exportExcel = null, bool quiet = false)
    {
        if (_main.SelectedDocument == null) { ShowToast("Chọn hợp đồng trước khi lập PDVT.", isError: true); return false; }
        if (!HasDraft) return false;
        if (DraftMaterials.Count == 0) { ShowToast("Phiếu chưa có vật tư nào.", isError: true); return false; }
        var number = string.IsNullOrWhiteSpace(DraftNumber) ? $"PDVT {PdvtService.NextNumber(_records):00}" : DraftNumber.Trim();
        var current = _editingRecord;
        if (_records.Any(x => x != current && string.Equals(x.Number, number, StringComparison.CurrentCultureIgnoreCase)))
        {
            ShowToast($"Số phiếu {number} đã tồn tại. Đổi số phiếu khác.", isError: true);
            return false;
        }

        var record = current ?? new PdvtRecord { DocumentId = _main.SelectedDocument.Id };
        record.Number = number;
        record.Date = DraftDate;
        record.SubmittedDate = DraftSubmittedDate;
        record.Version = DraftVersion;
        record.HạngMục = DraftCategory;
        record.NhaCungCap = DraftSupplier;
        record.Materials = DraftMaterials.Select(Clone).ToList();
        record.Status = record.DerivedStatus;
        if (current == null) _records.Add(record);
        _service.Save(_main.SelectedDocument.Id, _records);

        _editingRecord = record;
        _loadingDraft = true;
        DraftNumber = number;
        IsEditingExisting = true;
        _loadingDraft = false;
        IsDirty = false;
        Recompute();
        RefreshRecords();
        UpdateDraftStats();

        // Results do not change the sheet content, so only drafts regenerate Excel on a plain save.
        if (!(exportExcel ?? (openExcel || !IsLocked)))
        {
            if (!quiet) ShowToast($"Đã lưu {record.Number}.");
            return true;
        }
        try
        {
            record.ExcelPath = _service.ExportExcel(record, _main.SelectedDocument, _projectInfoProvider());
            _service.Save(_main.SelectedDocument.Id, _records);
            if (openExcel)
            {
                OpenPath(record.ExcelPath);
                ShowToast($"Đã lưu và mở {System.IO.Path.GetFileName(record.ExcelPath)}.");
            }
            else if (!quiet) ShowToast($"Đã lưu {record.Number} và cập nhật file Excel.");
        }
        catch (Exception ex)
        {
            ShowToast($"Đã lưu {record.Number}, nhưng chưa tạo được Excel: {ex.Message}", isError: true);
        }
        return true;
    }

    // ------------------------------------------------------------------ helpers

    private void ShowToast(string message, bool isError = false, bool canUndo = false)
    {
        StatusMessage = message;
        IsToastError = isError;
        CanUndo = canUndo && _undo.Count > 0;
        IsToastVisible = !string.IsNullOrEmpty(message);
        _toastTimer ??= CreateToastTimer();
        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(isError || canUndo ? 8 : 4);
        _toastTimer.Start();
    }

    private DispatcherTimer CreateToastTimer()
    {
        var timer = new DispatcherTimer();
        timer.Tick += (_, _) => { timer.Stop(); IsToastVisible = false; CanUndo = false; };
        return timer;
    }

    [RelayCommand]
    private void DismissToast()
    {
        _toastTimer?.Stop();
        IsToastVisible = false;
        CanUndo = false;
    }

    private static void RunLater(Action action) =>
        (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).BeginInvoke(action, DispatcherPriority.Input);

    private static void OpenPath(string path) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });

    private static PdvtMaterialRow Clone(PdvtMaterialRow row) => new()
    {
        Id = row.Id, Source = row.Source, BoqItemId = row.BoqItemId, NoiDung = row.NoiDung,
        NoiDungTheoHd = row.NoiDungTheoHd, ThongSoKyThuat = row.ThongSoKyThuat, ThongSoTheoHd = row.ThongSoTheoHd,
        ModelTheoHd = row.ModelTheoHd, NhanHieuTheoHd = row.NhanHieuTheoHd,
        XuatXuTheoHd = row.XuatXuTheoHd, XuatXuDeTrinh = row.XuatXuDeTrinh,
        NhanHieuPheDuyet = row.NhanHieuPheDuyet, Model = row.Model, DonViTinh = row.DonViTinh,
        DonGia = row.DonGia, KhoiLuong = row.KhoiLuong, Status = row.Status,
        ApprovalRound = row.ApprovalRound, ApprovedDate = row.ApprovedDate, Note = row.Note, ReviewNote = row.ReviewNote,
        NhanHieuOptions = row.NhanHieuOptions.ToList()
    };

    /// <summary>Sorts "PDVT 10" after "PDVT 9".</summary>
    private sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new();
        public int Compare(string? x, string? y)
        {
            var a = Regex.Split(x ?? string.Empty, @"(\d+)");
            var b = Regex.Split(y ?? string.Empty, @"(\d+)");
            for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                var result = long.TryParse(a[i], out var na) && long.TryParse(b[i], out var nb)
                    ? na.CompareTo(nb)
                    : string.Compare(a[i], b[i], StringComparison.CurrentCultureIgnoreCase);
                if (result != 0) return result;
            }
            return a.Length.CompareTo(b.Length);
        }
    }
}
