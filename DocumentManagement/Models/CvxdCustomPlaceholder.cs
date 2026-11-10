using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace DocumentManagement.Models;

public partial class CvxdCustomPlaceholder : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private string _scope = "Cả 2";

    [ObservableProperty]
    [JsonIgnore]
    private string _preview = string.Empty;

    [ObservableProperty]
    [JsonIgnore]
    private string _missingTokens = string.Empty;
}
