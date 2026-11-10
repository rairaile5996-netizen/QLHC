using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace DocumentManagement.Models;

public enum PdvtApprovalStatus { ChuaTrinh, DaTrinh, YeuCauSua, DaDuyet, KhongDuyet }

public sealed record PdvtApprovalStatusOption(PdvtApprovalStatus Value, string DisplayText);

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
}

public partial class PdvtMaterialRow : ObservableObject
{
    [JsonIgnore]
    [ObservableProperty] private int _stt;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Source { get; set; } = "BoQ";
    public string BoqItemId { get; set; } = string.Empty;
    [ObservableProperty] private string _noiDung = string.Empty;
    [ObservableProperty] private string _thongSoKyThuat = string.Empty;
    [ObservableProperty] private string _nhanHieuTheoHd = string.Empty;
    [ObservableProperty] private string _nhanHieuPheDuyet = string.Empty;
    [ObservableProperty] private string _noiDungTheoHd = string.Empty;
    [ObservableProperty] private string _thongSoTheoHd = string.Empty;
    [ObservableProperty] private string _modelTheoHd = string.Empty;
    [ObservableProperty] private string _xuatXuTheoHd = string.Empty;
    [ObservableProperty] private string _xuatXuDeTrinh = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _donViTinh = string.Empty;
    [ObservableProperty] private decimal _donGia;
    [ObservableProperty] private decimal _khoiLuong;
    [ObservableProperty] private PdvtApprovalStatus _status;
    [ObservableProperty] private string _approvalRound = string.Empty;
    [ObservableProperty] private DateTime? _approvedDate;
    [ObservableProperty] private string _note = string.Empty;
    public List<string> NhanHieuOptions { get; set; } = [];
    public string SourceLabel => Source == "BoQ" ? "Theo BoQ" : "Phát sinh";
    public string StatusLabel => Status switch { PdvtApprovalStatus.DaTrinh => "Đã trình", PdvtApprovalStatus.YeuCauSua => "Yêu cầu sửa", PdvtApprovalStatus.DaDuyet => "Đã duyệt", PdvtApprovalStatus.KhongDuyet => "Không duyệt", _ => "Chưa trình" };

    partial void OnThongSoTheoHdChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(ThongSoKyThuat))
        {
            ThongSoKyThuat = value;
        }
    }

    partial void OnNhanHieuTheoHdChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(NhanHieuPheDuyet))
        {
            NhanHieuPheDuyet = value;
        }
    }

    partial void OnXuatXuTheoHdChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(XuatXuDeTrinh))
        {
            XuatXuDeTrinh = value;
        }
    }

    partial void OnNoiDungTheoHdChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(NoiDung))
        {
            NoiDung = value;
        }
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
    public int Version { get; set; } = 1;
    public DateTime Date { get; set; } = DateTime.Today;
    public string HạngMục { get; set; } = string.Empty;
    public string NhaCungCap { get; set; } = string.Empty;
    public string ExcelPath { get; set; } = string.Empty;
    public PdvtApprovalStatus Status { get; set; }
    public List<PdvtMaterialRow> Materials { get; set; } = [];
    public List<PdvtApprovalHistory> History { get; set; } = [];
    public string StatusLabel => Status switch { PdvtApprovalStatus.DaDuyet => "Đã duyệt", PdvtApprovalStatus.DaTrinh => "Đã trình", PdvtApprovalStatus.YeuCauSua => "Yêu cầu sửa", PdvtApprovalStatus.KhongDuyet => "Không duyệt", _ => "Nháp" };
    public int ApprovedCount => Materials.Count(x => x.Status == PdvtApprovalStatus.DaDuyet);
}
