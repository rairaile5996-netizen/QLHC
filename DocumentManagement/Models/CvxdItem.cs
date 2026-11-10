using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace DocumentManagement.Models;

public partial class CvxdItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private string _tcnt = string.Empty;

    [ObservableProperty]
    private string _stt = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSoChecklistReadOnly))]
    [NotifyPropertyChangedFor(nameof(IsChecklistFieldEnabled))]
    private string _loaiNt = "CVXD";

    [ObservableProperty]
    private string _noiDung = string.Empty;

    [ObservableProperty]
    private string _ngayNt = DateTime.Now.ToString("dd/MM/yyyy");

    [ObservableProperty]
    private string _batDau = "09h00";

    [ObservableProperty]
    private string _ketThuc = "10h00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChecklistFieldEnabled))]
    private string _soChecklist = "1";

    public bool IsSoChecklistReadOnly => LoaiNt == "TA" || LoaiNt == "TK";
    public bool IsChecklistFieldEnabled => !IsSoChecklistReadOnly && SoChecklist == "1";

    [ObservableProperty]
    private string _soPdvt = string.Empty;

    [ObservableProperty]
    private string _loaiBb = string.Empty;

    [ObservableProperty]
    private string _checklistId = string.Empty; // Lưu prefix file (ví dụ: 01)

    [ObservableProperty]
    private string _tenNt = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _hasWarning;

    [ObservableProperty]
    private string _warningMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CvxdCustomFieldValue> _customFields = [];
}

public partial class CvxdCustomFieldValue : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}
