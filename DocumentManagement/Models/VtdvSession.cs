using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace DocumentManagement.Models;

public partial class VtdvSession : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _soBienBan = string.Empty;

    [ObservableProperty]
    private string _ngayNghiệmThu = DateTime.Now.ToString("dd/MM/yyyy");

    [ObservableProperty]
    private string _soPheDuyetVatTu = string.Empty;

    partial void OnNgayNghiệmThuChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        
        // Try to auto-format shorthand date like 2/3/26 -> 02/03/2026
        var parts = value.Split('/', '-', '.');
        if (parts.Length == 3)
        {
            if (int.TryParse(parts[0], out int day) && 
                int.TryParse(parts[1], out int month) && 
                int.TryParse(parts[2], out int year))
            {
                // Handle 2-digit year
                if (year < 100) year += 2000;
                
                try 
                {
                    var date = new DateTime(year, month, day);
                    var formatted = date.ToString("dd/MM/yyyy");
                    if (formatted != value)
                    {
                        _ngayNghiệmThu = formatted;
                        OnPropertyChanged(nameof(NgayNghiệmThu));
                    }
                }
                catch { /* Ignore invalid date */ }
            }
        }
    }

    [ObservableProperty]
    private string _doiTuongNghiệmThu = "Vật tư thiết bị đầu vào";

    [ObservableProperty]
    private string _duAn = string.Empty;

    [ObservableProperty]
    private string _congTrinh = string.Empty;

    [ObservableProperty]
    private string _hangMuc = string.Empty;

    [ObservableProperty]
    private string _nhaThau = string.Empty;

    [ObservableProperty]
    private string _batDau = "08h00";

    [ObservableProperty]
    private string _ketThuc = "10h00";

    [ObservableProperty]
    private string _diaDiem = "Tại công trường";

    [ObservableProperty]
    private ObservableCollection<VtdvItem> _items = [];

    [ObservableProperty]
    private ObservableCollection<CvxdCustomFieldValue> _customFields = [];
}
