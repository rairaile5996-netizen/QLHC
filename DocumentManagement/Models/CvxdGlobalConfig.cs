using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace DocumentManagement.Models;

public partial class CvxdGlobalConfig : ObservableObject
{
    [ObservableProperty]
    private string? _templateFolderPath;

    [ObservableProperty]
    private string? _checklistFolderPath;

    [ObservableProperty]
    private string? _hsplTemplateFolderPath;

    [ObservableProperty]
    private ObservableCollection<CvxdStandard> _globalStandards = [];

    [ObservableProperty]
    private int _standardsSeedVersion;

    [ObservableProperty]
    private ObservableCollection<CvxdCustomPlaceholder> _globalPlaceholders = [];
}
