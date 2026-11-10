using CommunityToolkit.Mvvm.ComponentModel;

namespace DocumentManagement.Models;

/// <summary>
/// Thông tin chung cho hồ sơ CVXD (nhập thủ công 1 lần cho mỗi hợp đồng).
/// Tương đương sheet "THÔNG TIN" trong file Excel gốc.
/// </summary>
public partial class CvxdInfo : ObservableObject
{
    [ObservableProperty]
    private string _templateProfile = string.Empty;

    public bool IsTvgsSectionEnabled => !string.Equals(TemplateProfile, "PKH", StringComparison.OrdinalIgnoreCase);
    public bool IsTvgsSectionLocked => !IsTvgsSectionEnabled;
    public bool IsEmployerSectionEnabled =>
        !string.Equals(TemplateProfile, "BÀ NÀ", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(TemplateProfile, "BA NA", StringComparison.OrdinalIgnoreCase);
    public bool IsEmployerSectionLocked => !IsEmployerSectionEnabled;

    partial void OnTemplateProfileChanged(string value)
    {
        OnPropertyChanged(nameof(IsTvgsSectionEnabled));
        OnPropertyChanged(nameof(IsTvgsSectionLocked));
        OnPropertyChanged(nameof(IsEmployerSectionEnabled));
        OnPropertyChanged(nameof(IsEmployerSectionLocked));
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public System.Collections.ObjectModel.ObservableCollection<string> TemplateProfiles { get; } = [];
    [ObservableProperty]
    private string _tenDuAn = string.Empty;        // {DuAn} Tên dự án

    [ObservableProperty]
    private string _tenChuDauTu = string.Empty;     // {4} Tên chủ đầu tư

    [ObservableProperty]
    private string _tenCongTrinh = string.Empty;    // {CongTrinh} Tên công trình

    [ObservableProperty]
    private string _tenHangMuc = string.Empty;      // {HangMuc} Tên hạng mục

    [ObservableProperty]
    private string _tenTuVanGiamSat = string.Empty;  // {5} Tên tư vấn giám sát

    [ObservableProperty]
    private string _tenNhaThau = string.Empty;       // {6} Tên nhà thầu thi công

    [ObservableProperty]
    private string _tenTuVanThietKe = string.Empty;  // {22} Tên tư vấn thiết kế

    [ObservableProperty]
    private string _daiDienChuDauTu = string.Empty;  // {12} Đại diện CĐT

    [ObservableProperty]
    private string _daiDienTVGS = string.Empty;      // {13} Đại diện TVGS

    [ObservableProperty]
    private string _daiDienNhaThau = string.Empty;  // {14} Đại diện Nhà thầu chính

    [ObservableProperty]
    private string _kyHieuTVGS = string.Empty;       // Ký hiệu công ty tư vấn giám sát

    [ObservableProperty]
    private string _kyHieuChuDauTu = string.Empty;   // Ký hiệu công ty bên giao thầu

    [ObservableProperty]
    private string _kyHieuNhaThau = string.Empty;    // {KY_HIEU_NT} Ký hiệu công ty bên nhận thầu

    [ObservableProperty]
    private string _tenNhaThauPhu = string.Empty;   // Tên đơn vị nhà thầu phụ
    
    [ObservableProperty]
    private string _daiDienNhaThauPhu = string.Empty; // {DaiDienNTP} Đại diện Nhà thầu phụ

    [ObservableProperty]
    private string _chucVuCDT = string.Empty;         // {18} Chức vụ CĐT

    [ObservableProperty]
    private string _diaDiem = string.Empty;           // {25} Địa điểm

    [ObservableProperty]
    private string _canCuThiCong = string.Empty;      // {27} Căn cứ thi công

    [ObservableProperty]
    private string _ngayKyHD = string.Empty;           // {NgayKy} Ngày ký hợp đồng

    [ObservableProperty]
    private string _chucVuTVGS = "Giám sát trưởng";    // {19} Chức vụ TVGS

    [ObservableProperty]
    private string _chucVuNT = "Chỉ huy trưởng";      // {28} Chức vụ Nhà thầu

    [ObservableProperty]
    private string _soHopDongDauTu = string.Empty;    // {24} Số HĐ hoặc Số BB phê duyệt vật liệu

    [ObservableProperty]
    private string _displayContractNumber = string.Empty;

    [ObservableProperty]
    private string _displaySignDate = string.Empty;

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<CvxdStandard> _standards = [];

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<CvxdCustomPlaceholder> _customPlaceholders = [];

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<CvxdCustomColumn> _customColumns = [];

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<CvxdCustomColumn> _vtdvCustomColumns = [];

    // Danh sach dai dien duoc luu theo tung hop dong. Nguoi ky duoc chon tu day
    // va dong bo vao cac truong dai dien cu de khong anh huong mau CVXD/VTĐV.
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<ContractRepresentative> _tvgsRepresentatives = [];

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<ContractRepresentative> _employerRepresentatives = [];

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<ContractRepresentative> _contractorRepresentatives = [];
}

public partial class ContractRepresentative : ObservableObject
{
    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _position = string.Empty;
}

public partial class CvxdCustomColumn : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
}
