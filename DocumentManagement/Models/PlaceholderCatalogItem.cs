using CommunityToolkit.Mvvm.ComponentModel;

namespace DocumentManagement.Models;

public partial class PlaceholderCatalogItem : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private string _scope = string.Empty;
}
