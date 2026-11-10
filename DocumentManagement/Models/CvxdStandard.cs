using CommunityToolkit.Mvvm.ComponentModel;

namespace DocumentManagement.Models;

public partial class CvxdStandard : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;   // Ví dụ: ĐIỆN

    [ObservableProperty]
    private string _value = string.Empty; // Ví dụ: TCVN 9206:2012...
}
