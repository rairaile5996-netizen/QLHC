using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace DocumentManagement.Models;

public partial class HsplPerson : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _position = string.Empty;
    [ObservableProperty] private string _construction = string.Empty;
    [ObservableProperty] private string _reportsToId = string.Empty;
    [JsonIgnore] public int RowNumber { get; set; }
}

public partial class HsplWorker : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _position = string.Empty;
    [ObservableProperty] private DateTime? _dateOfBirth;
    [JsonIgnore] public int RowNumber { get; set; }
}

public sealed class HsplLibraryProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<HsplPerson> Personnel { get; set; } = [];
    public List<HsplWorker> Workers { get; set; } = [];
}

public sealed class HsplPersonnelLibraryProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<HsplPerson> Personnel { get; set; } = [];
}

public sealed class HsplWorkerLibraryProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<HsplWorker> Workers { get; set; } = [];
}

public sealed class HsplTemplateOption
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
}

public partial class HsplDocumentTypeOption : ObservableObject
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    [ObservableProperty] private bool _isSelected;
}

public partial class HsplPageItem : ObservableObject
{
    public int? PageNumber { get; init; }
    public string Label => PageNumber?.ToString() ?? "…";
    public bool IsEllipsis => !PageNumber.HasValue;
    [ObservableProperty] private bool _isCurrent;
}

public sealed class HsplData
{
    public string TemplatePath { get; set; } = string.Empty;
    public DateTime? DocumentDate { get; set; }
    public DateTime? ConstructionStartDate { get; set; }
    public int WorkerCountForCcat { get; set; }
    public string ChosenCommanderId { get; set; } = string.Empty;
    public string ChosenAtldHeadId { get; set; } = string.Empty;
    public List<HsplPerson> Personnel { get; set; } = [];
    public List<HsplWorker> Workers { get; set; } = [];
}
