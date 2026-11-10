using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentManagement.Models;
using DocumentManagement.Services;
using DocumentManagement.Views;

namespace DocumentManagement.ViewModels;

public partial class HsplViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly HsplExportService _exportService;
    private readonly Func<string> _templateRootProvider;
    private readonly Func<CvxdInfo?> _projectInfoProvider;
    private readonly HsplLibraryService _libraryService = new();
    private HsplData _data = new();

    public ObservableCollection<Document> Documents => _mainViewModel.Documents;
    public ObservableCollection<HsplPerson> Personnel { get; } = [];
    public ObservableCollection<HsplPerson> PersonnelPage { get; } = [];
    public ObservableCollection<HsplWorker> Workers { get; } = [];
    public ObservableCollection<HsplWorker> WorkersPage { get; } = [];
    public ObservableCollection<HsplPageItem> PersonnelPages { get; } = [];
    public ObservableCollection<HsplPageItem> WorkersPages { get; } = [];
    public ObservableCollection<HsplTemplateOption> AvailableTemplates { get; } = [];
    public ObservableCollection<HsplPersonnelLibraryProfile> PersonnelLibraries { get; } = [];
    public ObservableCollection<HsplWorkerLibraryProfile> WorkerLibraries { get; } = [];
    public ObservableCollection<HsplDocumentTypeOption> DocumentTypes { get; } =
    [
        new() { Code = "PLNT", Name = "Pháp lý nhà thầu" },
        new() { Code = "CCAT", Name = "Cam kết an toàn lao động" },
        new() { Code = "DKC", Name = "Đơn khởi công" },
        new() { Code = "BGMB", Name = "Bàn giao mặt bằng" }
    ];

    [ObservableProperty] private Document? _selectedDocument;
    [ObservableProperty] private string _templatePath = string.Empty;
    [ObservableProperty] private HsplTemplateOption? _selectedTemplate;
    [ObservableProperty] private HsplPerson? _selectedPerson;
    [ObservableProperty] private HsplWorker? _selectedWorker;
    [ObservableProperty] private HsplPerson? _commander;
    [ObservableProperty] private HsplPerson? _atldHead;
    [ObservableProperty] private DateTime? _documentDate;
    [ObservableProperty] private DateTime? _constructionStartDate;
    [ObservableProperty] private int _workerCountForCcat;
    [ObservableProperty] private HsplPersonnelLibraryProfile? _selectedPersonnelLibrary;
    [ObservableProperty] private HsplWorkerLibraryProfile? _selectedWorkerLibrary;
    [ObservableProperty] private string _personnelLibraryName = string.Empty;
    [ObservableProperty] private string _workerLibraryName = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _loadingMessage = string.Empty;
    [ObservableProperty] private int _personnelPageNumber = 1;
    [ObservableProperty] private int _workersPageNumber = 1;

    private int _personnelPageSize = 5;
    private int _workersPageSize = 5;
    public int PersonnelPageCount => Math.Max(1, (Personnel.Count + _personnelPageSize - 1) / _personnelPageSize);
    public int WorkersPageCount => Math.Max(1, (Workers.Count + _workersPageSize - 1) / _workersPageSize);
    public string PersonnelPageSummary => $"Hiển thị {(Personnel.Count == 0 ? 0 : (PersonnelPageNumber - 1) * _personnelPageSize + 1)} - {Math.Min(PersonnelPageNumber * _personnelPageSize, Personnel.Count)} / {Personnel.Count} thành viên";
    public string WorkersPageSummary => $"Hiển thị {(Workers.Count == 0 ? 0 : (WorkersPageNumber - 1) * _workersPageSize + 1)} - {Math.Min(WorkersPageNumber * _workersPageSize, Workers.Count)} / {Workers.Count} công nhân";

    public void SetPersonnelPageSize(int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        if (_personnelPageSize == pageSize) return;
        var firstItemIndex = (PersonnelPageNumber - 1) * _personnelPageSize;
        _personnelPageSize = pageSize;
        PersonnelPageNumber = Math.Min(PersonnelPageCount, firstItemIndex / pageSize + 1);
        RefreshPages();
    }

    public void SetWorkersPageSize(int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        if (_workersPageSize == pageSize) return;
        var firstItemIndex = (WorkersPageNumber - 1) * _workersPageSize;
        _workersPageSize = pageSize;
        WorkersPageNumber = Math.Min(WorkersPageCount, firstItemIndex / pageSize + 1);
        RefreshPages();
    }
    public string SelectedTypesSummary => $"{DocumentTypes.Count(t => t.IsSelected)} / {DocumentTypes.Count} đã chọn";
    public string CreateButtonLabel => DocumentTypes.Count(t => t.IsSelected) == 0
        ? "CHỌN HỒ SƠ CẦN TẠO"
        : $"TẠO {DocumentTypes.Count(t => t.IsSelected)} HỒ SƠ";

    public string PersonnelStatusText => Personnel.Count > 0
        ? "✓  Đã nhập thông tin Ban chỉ huy"
        : "✕  Chưa nhập thông tin Ban chỉ huy";
    public Brush PersonnelStatusBrush => Personnel.Count > 0 ? Brushes.LimeGreen : Brushes.IndianRed;
    public string WorkersStatusText => Workers.Count > 0
        ? "✓  Đã nhập danh sách công nhân"
        : "✕  Chưa nhập danh sách công nhân";
    public Brush WorkersStatusBrush => Workers.Count > 0 ? Brushes.LimeGreen : Brushes.IndianRed;
    public string CcatStatusText => WorkerCountForCcat > 0 && WorkerCountForCcat <= Workers.Count
        ? $"✓  CCAT sẽ lấy {WorkerCountForCcat} / {Workers.Count} công nhân"
        : "✕  Chưa nhập số công nhân cần xuất CCAT";
    public Brush CcatStatusBrush => WorkerCountForCcat > 0 && WorkerCountForCcat <= Workers.Count ? Brushes.LimeGreen : Brushes.DarkOrange;
    public string SelectedTypesStatusText => DocumentTypes.Any(t => t.IsSelected)
        ? "✓  Đã chọn loại hồ sơ cần tạo"
        : "✕  Chưa chọn loại hồ sơ cần tạo";
    public Brush SelectedTypesStatusBrush => DocumentTypes.Any(t => t.IsSelected) ? Brushes.LimeGreen : Brushes.IndianRed;

    public HsplViewModel(
        MainViewModel mainViewModel,
        HsplExportService exportService,
        Func<string> templateRootProvider,
        Func<CvxdInfo?>? projectInfoProvider = null)
    {
        _mainViewModel = mainViewModel;
        _exportService = exportService;
        _templateRootProvider = templateRootProvider;
        _projectInfoProvider = projectInfoProvider ?? (() => null);
        Personnel.CollectionChanged += (_, _) => RefreshReadinessStatus();
        Workers.CollectionChanged += (_, _) => RefreshReadinessStatus();
        foreach (var type in DocumentTypes)
            type.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(HsplDocumentTypeOption.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedTypesSummary));
                    OnPropertyChanged(nameof(CreateButtonLabel));
                    RefreshReadinessStatus();
                }
            };
        SelectedDocument = _mainViewModel.SelectedDocument;
        RefreshPersonnelLibraries();
        RefreshWorkerLibraries();
        LoadForDocument();
    }

    partial void OnSelectedDocumentChanged(Document? value) => LoadForDocument();

    private void LoadForDocument()
    {
        Personnel.Clear();
        Workers.Clear();
        _data = SelectedDocument == null ? new HsplData() : _exportService.Load(SelectedDocument);
        TemplatePath = _data.TemplatePath;
        DocumentDate = _data.DocumentDate ?? DateTime.Today;
        ConstructionStartDate = _data.ConstructionStartDate;
        foreach (var person in _data.Personnel) Personnel.Add(person);
        foreach (var worker in _data.Workers) Workers.Add(worker);
        WorkerCountForCcat = _data.WorkerCountForCcat <= 0 ? Workers.Count : _data.WorkerCountForCcat;
        PersonnelPageNumber = 1;
        WorkersPageNumber = 1;
        RefreshPages();
        Commander = Personnel.FirstOrDefault(p => p.Id == _data.ChosenCommanderId);
        AtldHead = Personnel.FirstOrDefault(p => p.Id == _data.ChosenAtldHeadId);
        RefreshTemplates();
        StatusMessage = SelectedDocument == null ? "Chọn hợp đồng để bắt đầu." : $"Dữ liệu HSPL của {SelectedDocument.ContractNumber}";
        RefreshReadinessStatus();
    }

    partial void OnSelectedTemplateChanged(HsplTemplateOption? value)
    {
        TemplatePath = value?.FilePath ?? string.Empty;
        _data.TemplatePath = TemplatePath;
    }

    public void RefreshTemplates()
    {
        var previousPath = TemplatePath;
        AvailableTemplates.Clear();
        var root = _templateRootProvider();
        if (Directory.Exists(root))
        {
            var index = 1;
            foreach (var file in Directory.GetFiles(root, "*.docx", SearchOption.AllDirectories)
                         .Where(file => !Path.GetFileName(file).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(file => file, StringComparer.CurrentCultureIgnoreCase))
            {
                AvailableTemplates.Add(new HsplTemplateOption
                {
                    Id = index.ToString("00"),
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file
                });
                index++;
            }
        }

        SelectedTemplate = AvailableTemplates.FirstOrDefault(t => string.Equals(t.FilePath, previousPath, StringComparison.OrdinalIgnoreCase))
            ?? AvailableTemplates.FirstOrDefault();
        TemplatePath = SelectedTemplate?.FilePath ?? string.Empty;
    }

    [RelayCommand]
    private void AddPerson()
    {
        Personnel.Add(new HsplPerson { Construction = SelectedDocument?.ConstructionName ?? string.Empty });
        PersonnelPageNumber = PersonnelPageCount;
        RefreshPages();
    }

    [RelayCommand]
    private void RemovePerson(HsplPerson? person = null)
    {
        var target = person ?? SelectedPerson;
        if (target != null) Personnel.Remove(target);
        PersonnelPageNumber = Math.Min(PersonnelPageNumber, PersonnelPageCount);
        RefreshPages();
    }

    [RelayCommand]
    private void AddWorker()
    {
        Workers.Add(new HsplWorker());
        WorkersPageNumber = WorkersPageCount;
        RefreshPages();
    }

    [RelayCommand]
    private async Task ImportWorkersFromExcel()
    {
        var guide = new WorkerImportGuideWindow { Owner = Application.Current?.MainWindow };
        if (guide.ShowDialog() != true || string.IsNullOrWhiteSpace(guide.SelectedFilePath)) return;

        try
        {
            BeginLoading("Đang đọc file Excel...");
            var importedWorkers = await Task.Run(() => ReadWorkersFromExcel(guide.SelectedFilePath));
            await AppendWorkersAsync(importedWorkers);
            WorkerCountForCcat = Workers.Count;
            WorkersPageNumber = WorkersPageCount;
            RefreshPages();
            StatusMessage = $"Đã nạp thêm {importedWorkers.Count} công nhân từ Excel. Vui lòng kiểm tra trước khi lưu.";
        }
        catch (Exception ex)
        {
            CvxdMessageBox.Show("Excel", $"Không thể nạp danh sách công nhân từ Excel.\n\n{ex.Message}", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            EndLoading();
        }
    }

    [RelayCommand]
    private void RefreshPersonnelLibraries()
    {
        var selectedId = SelectedPersonnelLibrary?.Id;
        PersonnelLibraries.Clear();
        foreach (var profile in _libraryService.LoadPersonnel().OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase))
            PersonnelLibraries.Add(profile);
        SelectedPersonnelLibrary = PersonnelLibraries.FirstOrDefault(profile => profile.Id == selectedId);
    }

    [RelayCommand]
    private void RefreshWorkerLibraries()
    {
        var selectedId = SelectedWorkerLibrary?.Id;
        WorkerLibraries.Clear();
        foreach (var profile in _libraryService.LoadWorkers().OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase))
            WorkerLibraries.Add(profile);
        SelectedWorkerLibrary = WorkerLibraries.FirstOrDefault(profile => profile.Id == selectedId);
    }

    [RelayCommand]
    private async Task LoadPersonnelLibrary()
    {
        if (SelectedPersonnelLibrary == null)
        {
            StatusMessage = "Chọn mẫu Ban chỉ huy cần nạp.";
            return;
        }

        try
        {
            var profile = SelectedPersonnelLibrary;
            BeginLoading($"Đang nạp mẫu Ban chỉ huy {profile.Name}...");
            var people = await Task.Run(() => profile.Personnel.Select(ClonePerson).ToList());
            await ReplacePersonnelAsync(people);
        }
        finally
        {
            EndLoading();
        }
        Commander = null;
        AtldHead = null;
        PersonnelPageNumber = 1;
        RefreshPages();
        StatusMessage = $"Đã nạp mẫu Ban chỉ huy {SelectedPersonnelLibrary.Name}. Hãy chọn lại Chỉ huy trưởng và Trưởng ban ATLĐ nếu cần.";
    }

    [RelayCommand]
    private async Task LoadWorkerLibrary()
    {
        if (SelectedWorkerLibrary == null)
        {
            StatusMessage = "Chọn mẫu công nhân cần nạp.";
            return;
        }

        try
        {
            var profile = SelectedWorkerLibrary;
            BeginLoading($"Đang nạp mẫu công nhân {profile.Name}...");
            var workers = await Task.Run(() => profile.Workers.Select(CloneWorker).ToList());
            await ReplaceWorkersAsync(workers);
        }
        finally
        {
            EndLoading();
        }
        WorkerCountForCcat = Workers.Count;
        WorkersPageNumber = 1;
        RefreshPages();
        StatusMessage = $"Đã nạp mẫu công nhân {SelectedWorkerLibrary.Name}.";
    }

    [RelayCommand]
    private void SavePersonnelLibrary()
    {
        var name = PersonnelLibraryName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Nhập tên mẫu Ban chỉ huy trước khi lưu.";
            return;
        }

        var profiles = _libraryService.LoadPersonnel();
        var existing = profiles.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.CurrentCultureIgnoreCase));
        var profile = existing ?? new HsplPersonnelLibraryProfile { Name = name };
        profile.Personnel = Personnel.Select(ClonePerson).ToList();
        if (existing == null) profiles.Add(profile);
        _libraryService.SavePersonnel(profiles);
        RefreshPersonnelLibraries();
        SelectedPersonnelLibrary = PersonnelLibraries.FirstOrDefault(item => item.Id == profile.Id);
        StatusMessage = $"Đã lưu mẫu Ban chỉ huy {name}.";
    }

    [RelayCommand]
    private void SaveWorkerLibrary()
    {
        var name = WorkerLibraryName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Nhập tên mẫu công nhân trước khi lưu.";
            return;
        }

        var profiles = _libraryService.LoadWorkers();
        var existing = profiles.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.CurrentCultureIgnoreCase));
        var profile = existing ?? new HsplWorkerLibraryProfile { Name = name };
        profile.Workers = Workers.Select(CloneWorker).ToList();
        if (existing == null) profiles.Add(profile);
        _libraryService.SaveWorkers(profiles);
        RefreshWorkerLibraries();
        SelectedWorkerLibrary = WorkerLibraries.FirstOrDefault(item => item.Id == profile.Id);
        StatusMessage = $"Đã lưu mẫu công nhân {name}.";
    }

    [RelayCommand]
    private void RemoveWorker(HsplWorker? worker = null)
    {
        var target = worker ?? SelectedWorker;
        if (target != null) Workers.Remove(target);
        WorkersPageNumber = Math.Min(WorkersPageNumber, WorkersPageCount);
        RefreshPages();
    }

    partial void OnPersonnelPageNumberChanged(int value) => RefreshPages();
    partial void OnWorkersPageNumberChanged(int value) => RefreshPages();

    [RelayCommand]
    private void PreviousPersonnelPage() => PersonnelPageNumber = Math.Max(1, PersonnelPageNumber - 1);

    [RelayCommand]
    private void NextPersonnelPage() => PersonnelPageNumber = Math.Min(PersonnelPageCount, PersonnelPageNumber + 1);

    [RelayCommand]
    private void FirstPersonnelPage() => PersonnelPageNumber = 1;

    [RelayCommand]
    private void LastPersonnelPage() => PersonnelPageNumber = PersonnelPageCount;

    [RelayCommand]
    private void PreviousWorkersPage() => WorkersPageNumber = Math.Max(1, WorkersPageNumber - 1);

    [RelayCommand]
    private void NextWorkersPage() => WorkersPageNumber = Math.Min(WorkersPageCount, WorkersPageNumber + 1);

    [RelayCommand]
    private void FirstWorkersPage() => WorkersPageNumber = 1;

    [RelayCommand]
    private void LastWorkersPage() => WorkersPageNumber = WorkersPageCount;

    [RelayCommand]
    private void GoToPersonnelPage(int page) => PersonnelPageNumber = Math.Clamp(page, 1, PersonnelPageCount);

    [RelayCommand]
    private void GoToWorkersPage(int page) => WorkersPageNumber = Math.Clamp(page, 1, WorkersPageCount);

    private void RefreshPages()
    {
        PersonnelPage.Clear();
        foreach (var item in Personnel.Skip((PersonnelPageNumber - 1) * _personnelPageSize).Take(_personnelPageSize).Select((item, index) => (item, index)))
        {
            item.item.RowNumber = (PersonnelPageNumber - 1) * _personnelPageSize + item.index + 1;
            PersonnelPage.Add(item.item);
        }
        WorkersPage.Clear();
        foreach (var item in Workers.Skip((WorkersPageNumber - 1) * _workersPageSize).Take(_workersPageSize).Select((item, index) => (item, index)))
        {
            item.item.RowNumber = (WorkersPageNumber - 1) * _workersPageSize + item.index + 1;
            WorkersPage.Add(item.item);
        }
        OnPropertyChanged(nameof(PersonnelPageCount));
        OnPropertyChanged(nameof(WorkersPageCount));
        OnPropertyChanged(nameof(PersonnelPageSummary));
        OnPropertyChanged(nameof(WorkersPageSummary));
        RefreshPageItems(PersonnelPages, PersonnelPageCount, PersonnelPageNumber);
        RefreshPageItems(WorkersPages, WorkersPageCount, WorkersPageNumber);
        OnPropertyChanged(nameof(WorkersForCcatSummary));
    }

    private static void RefreshPageItems(ObservableCollection<HsplPageItem> target, int pageCount, int currentPage)
    {
        target.Clear();
        if (pageCount <= 7)
        {
            for (var page = 1; page <= pageCount; page++) target.Add(new HsplPageItem { PageNumber = page, IsCurrent = page == currentPage });
            return;
        }

        target.Add(new HsplPageItem { PageNumber = 1, IsCurrent = currentPage == 1 });
        var start = Math.Max(2, currentPage - 1);
        var end = Math.Min(pageCount - 1, currentPage + 1);
        if (start > 2) target.Add(new HsplPageItem());
        for (var page = start; page <= end; page++) target.Add(new HsplPageItem { PageNumber = page, IsCurrent = page == currentPage });
        if (end < pageCount - 1) target.Add(new HsplPageItem());
        target.Add(new HsplPageItem { PageNumber = pageCount, IsCurrent = currentPage == pageCount });
    }

    [RelayCommand]
    private void SaveData()
    {
        if (SelectedDocument == null) return;
        _data.TemplatePath = TemplatePath;
        _data.DocumentDate = DocumentDate;
        _data.ConstructionStartDate = ConstructionStartDate;
        _data.WorkerCountForCcat = WorkerCountForCcat;
        _data.Personnel = Personnel.ToList();
        _data.Workers = Workers.ToList();
        _data.ChosenCommanderId = Commander?.Id ?? string.Empty;
        _data.ChosenAtldHeadId = AtldHead?.Id ?? string.Empty;
        _exportService.Save(SelectedDocument, _data);
        StatusMessage = "Đã lưu cấu hình HSPL cho hợp đồng.";
        CvxdMessageBox.Show("Thành công", "Đã lưu dữ liệu HSPL cho hợp đồng thành công.", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void Export()
    {
        if (SelectedDocument == null) { CvxdMessageBox.Show("HSPL", "Vui lòng chọn hợp đồng.", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var selectedTypes = DocumentTypes.Where(t => t.IsSelected).ToList();
        if (selectedTypes.Count == 0)
        {
            CvxdMessageBox.Show("HSPL", "Vui lòng chọn ít nhất một loại hồ sơ cần tạo.", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (Personnel.Count == 0)
        {
            CvxdMessageBox.Show("HSPL", "Vui lòng nhập danh sách Ban chỉ huy trước khi tạo HSPL.", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (Personnel.Any(person => string.IsNullOrWhiteSpace(person.FullName) || string.IsNullOrWhiteSpace(person.Position)))
        {
            CvxdMessageBox.Show("HSPL", "Vui lòng nhập đầy đủ họ tên và chức vụ cho từng thành viên Ban chỉ huy.", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var selectedWorkers = Workers.Take(WorkerCountForCcat).ToList();
        if (selectedTypes.Any(type => type.Code == "CCAT"))
        {
            if (WorkerCountForCcat <= 0)
            {
                CvxdMessageBox.Show("HSPL", "Nhập số công nhân cần xuất Cam kết an toàn lao động.", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (WorkerCountForCcat > Workers.Count)
            {
                CvxdMessageBox.Show("HSPL", $"Danh sách hiện có {Workers.Count} công nhân, không thể xuất {WorkerCountForCcat} công nhân.", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (selectedWorkers.Any(worker => string.IsNullOrWhiteSpace(worker.FullName)
                                      || string.IsNullOrWhiteSpace(worker.Position)
                                      || !worker.DateOfBirth.HasValue))
            {
                CvxdMessageBox.Show("HSPL", "Vui lòng nhập đủ họ tên, chức vụ/nghề và ngày sinh cho từng công nhân trước khi tạo Cam kết an toàn lao động.", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }
        SaveData();
        try
        {
            var exportData = new HsplData
            {
                TemplatePath = _data.TemplatePath,
                DocumentDate = _data.DocumentDate,
                ConstructionStartDate = _data.ConstructionStartDate,
                ChosenCommanderId = _data.ChosenCommanderId,
                ChosenAtldHeadId = _data.ChosenAtldHeadId,
                Personnel = _data.Personnel,
                Workers = selectedWorkers
            };
            var outputs = _exportService.ExportSelected(
                SelectedDocument,
                exportData,
                selectedTypes,
                AvailableTemplates,
                _projectInfoProvider());
            StatusMessage = $"Đã tạo {outputs.Count} hồ sơ HSPL.";
            CvxdMessageBox.Show("Xuất HSPL thành công", $"Đã tạo {outputs.Count} hồ sơ HSPL:\n\n{string.Join("\n", outputs)}", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            CvxdMessageBox.Show("Không thể xuất HSPL", ex.Message, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    partial void OnWorkerCountForCcatChanged(int value)
    {
        OnPropertyChanged(nameof(WorkersForCcatSummary));
        RefreshReadinessStatus();
    }

    private void RefreshReadinessStatus()
    {
        OnPropertyChanged(nameof(PersonnelStatusText));
        OnPropertyChanged(nameof(PersonnelStatusBrush));
        OnPropertyChanged(nameof(WorkersStatusText));
        OnPropertyChanged(nameof(WorkersStatusBrush));
        OnPropertyChanged(nameof(CcatStatusText));
        OnPropertyChanged(nameof(CcatStatusBrush));
        OnPropertyChanged(nameof(SelectedTypesStatusText));
        OnPropertyChanged(nameof(SelectedTypesStatusBrush));
    }

    public string WorkersForCcatSummary => WorkerCountForCcat <= 0
        ? "Nhập số công nhân cần xuất CCAT"
        : $"CCAT sẽ lấy công nhân dòng 1 - {Math.Min(WorkerCountForCcat, Workers.Count)} / {Workers.Count}";

    private static HsplPerson ClonePerson(HsplPerson person) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        FullName = person.FullName,
        Position = person.Position,
        Construction = person.Construction,
        ReportsToId = string.Empty
    };

    private static HsplWorker CloneWorker(HsplWorker worker) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        FullName = worker.FullName,
        Position = worker.Position,
        DateOfBirth = worker.DateOfBirth
    };

    private static int? FindColumn(IEnumerable<IXLCell> cells, params string[] candidates)
    {
        foreach (var cell in cells)
        {
            var header = NormalizeHeader(cell.GetString());
            if (candidates.Any(candidate => header.Contains(NormalizeHeader(candidate), StringComparison.Ordinal)))
                return cell.Address.ColumnNumber;
        }
        return null;
    }

    private static string NormalizeHeader(string value) => value
        .Trim()
        .ToLowerInvariant()
        .Replace("đ", "d")
        .Replace("/", " ")
        .Replace("_", " ");

    private static DateTime? ReadExcelDate(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime();
        var value = cell.GetString().Trim();
        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out var date)) return date;
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial)) return DateTime.FromOADate(serial);
        return null;
    }

    private void BeginLoading(string message)
    {
        LoadingMessage = message;
        IsLoading = true;
        StatusMessage = message;
    }

    private void EndLoading()
    {
        IsLoading = false;
        LoadingMessage = string.Empty;
    }

    private async Task ReplacePersonnelAsync(IReadOnlyList<HsplPerson> people)
    {
        Personnel.Clear();
        for (var index = 0; index < people.Count; index++)
        {
            Personnel.Add(people[index]);
            if ((index + 1) % 100 == 0)
            {
                LoadingMessage = $"Đang nạp Ban chỉ huy: {index + 1:N0} / {people.Count:N0} thành viên...";
                await Task.Yield();
            }
        }
    }

    private async Task ReplaceWorkersAsync(IReadOnlyList<HsplWorker> workers)
    {
        Workers.Clear();
        await AppendWorkersAsync(workers, true);
    }

    private async Task AppendWorkersAsync(IReadOnlyList<HsplWorker> workers, bool isReplacing = false)
    {
        var initialCount = Workers.Count;
        for (var index = 0; index < workers.Count; index++)
        {
            Workers.Add(workers[index]);
            if ((index + 1) % 100 == 0)
            {
                LoadingMessage = $"Đang {(isReplacing ? "nạp" : "thêm")} công nhân: {index + 1:N0} / {workers.Count:N0}...";
                await Task.Yield();
            }
        }

        if (workers.Count > 0)
            LoadingMessage = $"Đã xử lý {Workers.Count - initialCount:N0} công nhân.";
    }

    private static List<HsplWorker> ReadWorkersFromExcel(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("File Excel không có trang tính.");
        var usedRange = sheet.RangeUsed()
            ?? throw new InvalidOperationException("File Excel không có dữ liệu.");
        var headerRow = usedRange.FirstRow();
        var headerCells = headerRow.CellsUsed().ToList();
        var nameColumn = FindColumn(headerCells, "họ tên", "họ và tên", "tên công nhân", "ho ten") ?? 1;
        var positionColumn = FindColumn(headerCells, "chức vụ", "nghề", "chức vụ / nghề", "chuc vu") ?? 2;
        var birthColumn = FindColumn(headerCells, "ngày sinh", "năm sinh", "ngay sinh") ?? 3;
        var looksLikeHeader = headerCells.Any(cell => NormalizeHeader(cell.GetString()).Contains("tên"))
                              || headerCells.Any(cell => NormalizeHeader(cell.GetString()).Contains("sinh"));
        var firstDataRow = headerRow.RowNumber() + (looksLikeHeader ? 1 : 0);
        var workers = new List<HsplWorker>();

        foreach (var row in sheet.RowsUsed().Where(row => row.RowNumber() >= firstDataRow))
        {
            var fullName = row.Cell(nameColumn).GetString().Trim();
            if (string.IsNullOrWhiteSpace(fullName)) continue;
            workers.Add(new HsplWorker
            {
                FullName = fullName,
                Position = row.Cell(positionColumn).GetString().Trim(),
                DateOfBirth = ReadExcelDate(row.Cell(birthColumn))
            });
        }

        return workers.Count == 0
            ? throw new InvalidOperationException("Không tìm thấy dòng công nhân hợp lệ trong file Excel.")
            : workers;
    }
}
