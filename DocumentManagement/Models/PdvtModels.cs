using CommunityToolkit.Mvvm.ComponentModel;
using DocumentManagement.Services;
using System.Text.Json.Serialization;

namespace DocumentManagement.Models;

/// <summary>Per-material result. ChuaTrinh = still in a draft sheet; DaTrinh = submitted, waiting for a result.</summary>
public enum PdvtApprovalStatus { ChuaTrinh, DaTrinh, YeuCauSua, DaDuyet, KhongDuyet }

/// <summary>Where a BoQ material stands across every PDVT round that contains it.</summary>
public enum PdvtBoqProgress { ChuaLap, ChoKetQua, CanTrinhLai, DaDuyet }

public sealed record PdvtApprovalStatusOption(PdvtApprovalStatus Value, string DisplayText);

/// <summary>One submission of a BoQ material, shown in the summary detail timeline.</summary>
public sealed record PdvtHistoryEntry(string RecordId, string Reference, string Submitted, string Brand, PdvtApprovalStatus Status, bool IsSubmitted, string Note)
{
    public string StatusLabel => PdvtProgress.StatusLabel(Status, IsSubmitted);
    public string StatusKind => PdvtProgress.StatusKind(Status, IsSubmitted);
}

public partial class PdvtBoqItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SttBoq { get; set; } = string.Empty;
    public bool IsHeading { get; set; }
    public int HeadingLevel { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string NoiDung { get; set; } = string.Empty;
    public string ThongSoKyThuat { get; set; } = string.Empty;
    public string NhanHieuXuatXu { get; set; } = string.Empty;
    public string NhanHieuTheoHd { get; set; } = string.Empty;
    public string XuatXuTheoHd { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string DonViTinh { get; set; } = string.Empty;
    public decimal DonGia { get; set; }
    public decimal KhoiLuong { get; set; }
    public List<string> NhanHieuOptions { get; set; } = [];
    [ObservableProperty] private bool _isSelected;
    [JsonIgnore]
    [ObservableProperty] private string _pdvtNumbers = string.Empty;
    [JsonIgnore]
    [ObservableProperty] private string _approvedPdvtNumbers = string.Empty;
    [JsonIgnore]
    [ObservableProperty] private string _approvalSummary = "Chưa trình";

    // ---- computed by PdvtProgress.Apply, never persisted ----
    [JsonIgnore][ObservableProperty] private PdvtBoqProgress _progress;
    [JsonIgnore][ObservableProperty] private string _statusLabel = "Chưa lập";
    [JsonIgnore][ObservableProperty] private string _statusKind = "none";
    [JsonIgnore][ObservableProperty] private string _latestReference = string.Empty;
    [JsonIgnore][ObservableProperty] private string _latestRecordId = string.Empty;
    [JsonIgnore][ObservableProperty] private string _approvedBrand = string.Empty;
    [JsonIgnore][ObservableProperty] private string _approvedOrigin = string.Empty;
    [JsonIgnore][ObservableProperty] private DateTime? _approvedDate;
    [JsonIgnore][ObservableProperty] private bool _isInDraft;
    [JsonIgnore][ObservableProperty] private List<PdvtHistoryEntry> _history = [];
    // Heading rows: material counts inside the section.
    [JsonIgnore][ObservableProperty] private int _childTotal;
    [JsonIgnore][ObservableProperty] private int _childApproved;
    [JsonIgnore][ObservableProperty] private int _childPending;
    [JsonIgnore][ObservableProperty] private int _childRework;
    [JsonIgnore][ObservableProperty] private int _childUnassigned;

    [JsonIgnore] public string BrandOriginDisplay => string.Join(" · ", new[] { NhanHieuTheoHd, XuatXuTheoHd }.Where(x => !string.IsNullOrWhiteSpace(x)));
    [JsonIgnore] public string SpecLine => string.Join(" · ", new[] { ThongSoKyThuat, BrandOriginDisplay }.Where(x => !string.IsNullOrWhiteSpace(x)));
    [JsonIgnore] public bool IsTopHeading => IsHeading && HeadingLevel <= 1;
}

public partial class PdvtMaterialRow : ObservableObject
{
    [JsonIgnore]
    [ObservableProperty] private int _stt;
    [JsonIgnore]
    [ObservableProperty] private bool _isChecked;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Source { get; set; } = "BoQ";
    public string BoqItemId { get; set; } = string.Empty;
    [ObservableProperty] private string _noiDung = string.Empty;
    [ObservableProperty] private string _thongSoKyThuat = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(IsBrandDiff), nameof(IsDiff))] private string _nhanHieuTheoHd = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(IsBrandDiff), nameof(IsDiff))] private string _nhanHieuPheDuyet = string.Empty;
    [ObservableProperty] private string _noiDungTheoHd = string.Empty;
    [ObservableProperty] private string _thongSoTheoHd = string.Empty;
    [ObservableProperty] private string _modelTheoHd = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(IsOriginDiff), nameof(IsDiff))] private string _xuatXuTheoHd = string.Empty;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(IsOriginDiff), nameof(IsDiff))] private string _xuatXuDeTrinh = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _donViTinh = string.Empty;
    [ObservableProperty] private decimal _donGia;
    [ObservableProperty] private decimal _khoiLuong;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(StatusLabel), nameof(HasResult))] private PdvtApprovalStatus _status;
    [ObservableProperty] private string _approvalRound = string.Empty;
    [ObservableProperty] private DateTime? _approvedDate;
    /// <summary>Contractor note, exported to the Excel sheet.</summary>
    [ObservableProperty] private string _note = string.Empty;
    /// <summary>Supervisor (TVGS) feedback recorded with the result.</summary>
    [ObservableProperty] private string _reviewNote = string.Empty;
    public List<string> NhanHieuOptions { get; set; } = [];

    [JsonIgnore] public bool IsExtra => Source != "BoQ";
    [JsonIgnore] public string SourceLabel => IsExtra ? "Phát sinh" : "Theo BoQ";
    [JsonIgnore] public string StatusLabel => PdvtProgress.StatusLabel(Status, Status != PdvtApprovalStatus.ChuaTrinh);
    [JsonIgnore] public bool HasResult => PdvtProgress.HasResult(Status);
    [JsonIgnore] public bool IsBrandDiff => !IsExtra && PdvtProgress.IsDifferent(NhanHieuTheoHd, NhanHieuPheDuyet, NhanHieuOptions);
    [JsonIgnore] public bool IsOriginDiff => !IsExtra && PdvtProgress.IsDifferent(XuatXuTheoHd, XuatXuDeTrinh, []);
    [JsonIgnore] public bool IsDiff => IsBrandDiff || IsOriginDiff;

    partial void OnThongSoTheoHdChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(ThongSoKyThuat)) ThongSoKyThuat = value;
    }

    partial void OnNhanHieuTheoHdChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(NhanHieuPheDuyet)) NhanHieuPheDuyet = value;
    }

    partial void OnXuatXuTheoHdChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(XuatXuDeTrinh)) XuatXuDeTrinh = value;
    }

    partial void OnNoiDungTheoHdChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(NoiDung)) NoiDung = value;
    }
}

public sealed class PdvtApprovalHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string MaterialId { get; set; } = string.Empty;
    public string PdvtNumber { get; set; } = string.Empty;
    public PdvtApprovalStatus Status { get; set; }
    public string ApprovedOption { get; set; } = string.Empty;
    public DateTime? ApprovedDate { get; set; }
    public string Note { get; set; } = string.Empty;
}

public sealed class PdvtRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int DocumentId { get; set; }
    public string Number { get; set; } = string.Empty;
    /// <summary>Submission round (Lần trình). Round 2+ is created from a sheet that had rejected rows.</summary>
    public int Version { get; set; } = 1;
    /// <summary>The sheet this round re-submits, empty for round 1.</summary>
    public string PreviousRecordId { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    /// <summary>Null while the sheet is a draft.</summary>
    public DateTime? SubmittedDate { get; set; }
    public string HạngMục { get; set; } = string.Empty;
    public string NhaCungCap { get; set; } = string.Empty;
    public string ExcelPath { get; set; } = string.Empty;
    /// <summary>Persisted snapshot of <see cref="PdvtProgress.RecordStatus"/> for older readers.</summary>
    public PdvtApprovalStatus Status { get; set; }
    public List<PdvtMaterialRow> Materials { get; set; } = [];
    public List<PdvtApprovalHistory> History { get; set; } = [];

    [JsonIgnore] public bool IsSubmitted => SubmittedDate != null;
    [JsonIgnore] public PdvtApprovalStatus DerivedStatus => PdvtProgress.RecordStatus(this);
    [JsonIgnore] public string StatusLabel => PdvtProgress.RecordStatusLabel(this);
    [JsonIgnore] public string StatusKind => PdvtProgress.StatusKind(DerivedStatus, IsSubmitted);
    [JsonIgnore] public int ApprovedCount => Materials.Count(x => x.Status == PdvtApprovalStatus.DaDuyet);
    [JsonIgnore] public int ReworkCount => Materials.Count(x => x.Status == PdvtApprovalStatus.YeuCauSua);
    [JsonIgnore] public int RejectedCount => Materials.Count(x => x.Status == PdvtApprovalStatus.KhongDuyet);
    [JsonIgnore] public int PendingCount => Materials.Count - ApprovedCount - ReworkCount - RejectedCount;
    [JsonIgnore] public string RoundLabel => Version > 1 ? $"Lần {Version}" : string.Empty;
    /// <summary>Number of the round that re-submitted this sheet's rejected rows; set by the view model.</summary>
    [JsonIgnore] public string NextRoundNumber { get; set; } = string.Empty;
    [JsonIgnore] public string Subtitle => string.Join(" · ", new[]
    {
        string.IsNullOrWhiteSpace(HạngMục) ? "Chưa có hạng mục" : HạngMục,
        string.IsNullOrWhiteSpace(NhaCungCap) ? $"{Materials.Count} vật tư" : NhaCungCap
    });
    [JsonIgnore] public string ProgressText => PdvtProgress.RecordProgressText(this);
}
