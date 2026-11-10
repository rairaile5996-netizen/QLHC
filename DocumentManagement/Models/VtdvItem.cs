using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace DocumentManagement.Models;

public partial class VtdvItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SttNumber))]
    private string _stt = string.Empty;

    public int SttNumber => int.TryParse(Stt, out var value) ? value : 0;

    [ObservableProperty]
    private string _doiTuong = string.Empty; // Tên và loại sản phẩm / Đối tượng nghiệm thu

    [ObservableProperty]
    private string _soPhieuXuat = string.Empty; // Phiếu xuất số

    [ObservableProperty]
    private string _soLuong = string.Empty; // Khối lượng / Số lượng

    [ObservableProperty]
    private string _donVi = string.Empty;

    [ObservableProperty]
    private string _thuongHieu = string.Empty; // Thương hiệu / Nhãn hiệu / Xuất xứ

    [ObservableProperty]
    private string _maSanPham = string.Empty;

    [ObservableProperty]
    private string _chatLuong = string.Empty; // Chất lượng / Màu sắc / Giấy xuất xưởng (CO,CQ)

    [ObservableProperty]
    private string _ghiChu = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CvxdCustomFieldValue> _customFields = [];
}
