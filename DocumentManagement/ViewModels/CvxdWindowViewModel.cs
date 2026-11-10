using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Diagnostics;
using DocumentManagement;
using DocumentManagement.Models;
using DocumentManagement.Views;
using DocumentManagement.Services;
using DocumentManagement.Utilities;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Windows.Data;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;





namespace DocumentManagement.ViewModels

{

    public partial class CvxdWindowViewModel : ObservableObject, IDisposable

{

    private readonly IAuthService _authService;



    public bool IsReadOnly => _authService.IsGuest;

    public bool IsAdmin => _authService.IsAdmin;

    public User? CurrentUser => _authService.CurrentUser;



    private bool CanExecuteAdmin() => _authService.IsAdmin;

    private bool CanExecuteAdmin(object? _) => _authService.IsAdmin;

    [ObservableProperty]

    private ObservableCollection<Document> _documents = [];



    [ObservableProperty]

    private bool _allSelected = true;



    [ObservableProperty]

    private Document? _selectedDocument;



    [ObservableProperty]

    private Document? _pendingSelectedDocument;



    [ObservableProperty]

    private ObservableCollection<CvxdItem> _cvxdItems = [];



    [ObservableProperty]

    private CvxdInfo _cvxdInfo = new();



    [ObservableProperty]

    private CvxdItem? _selectedCvxdItem;



    [ObservableProperty]

    private ObservableCollection<ChecklistTemplateItem> _availableChecklists = [];



    [ObservableProperty]

    private ObservableCollection<WordTemplateItem> _availableWordTemplates = [];

    [ObservableProperty]
    private ObservableCollection<OtherTemplateItem> _availableOtherTemplates = [];

    public ObservableCollection<OtherTemplateItem> SelectedOtherTemplates { get; } = [];

    public bool HasSelectedOtherTemplates => SelectedOtherTemplates.Count > 0;
    public int OtherTemplateCount => AvailableOtherTemplates.Count;
    public int SelectedOtherTemplateCount => SelectedOtherTemplates.Count;

    [ObservableProperty]
    private string _otherTemplateSearch = string.Empty;

    [ObservableProperty]
    private bool _showSelectedOtherTemplatesOnly;

    public ICollectionView OtherTemplatesView { get; private set; } = null!;



    [ObservableProperty]

    private ObservableCollection<string> _availableNtTypes = [];

    public string SelectedTemplatePath => TemplateProfileResolver.GetTemplateRoot(GlobalConfig, CvxdInfo?.TemplateProfile);

    public string SelectedChecklistPath => TemplateProfileResolver.GetChecklistRoot(GlobalConfig, CvxdInfo?.TemplateProfile);

    public string SelectedHsplTemplatePath => TemplateProfileResolver.GetHsplRoot(GlobalConfig, CvxdInfo?.TemplateProfile);

    public string SelectedOtherTemplatePath => TemplateProfileResolver.GetOtherRoot(GlobalConfig, CvxdInfo?.TemplateProfile);

    [ObservableProperty]
    private DateTime? _otherDocumentDate;



    [ObservableProperty]

    private MainViewModel _mainVM;



    [ObservableProperty]

    private VtdvViewModel? _vtdvViewModel;

    [ObservableProperty]
    private HsplViewModel? _hsplViewModel;

    [ObservableProperty]
    private PdvtViewModel? _pdvtViewModel;

    [ObservableProperty]
    private SharedProjectInfoTestViewModel _sharedProjectInfoTest = new();



    // Tab hiện tại: 0 = Danh sách CVXD, 1 = Danh sách VTĐV, 2 = Thông tin chung, 3 = Checklist, 4 = Tiêu chuẩn

    [ObservableProperty]

    private bool _isDirty;



    [ObservableProperty]

    private int _selectedTabIndex;



    private bool _isSyncingSelection = false;



    [ObservableProperty]

    private bool _isBusy;



    [ObservableProperty]

    private string _statusMessage = string.Empty;



    [ObservableProperty]

    private string _toastMessage = string.Empty;



    [ObservableProperty]

    private bool _isToastVisible;



    [ObservableProperty]

    private string _searchText = string.Empty;



    [ObservableProperty]

    private double _progressValue;



    private static readonly JsonSerializerOptions _jsonOptions = new()

    {

        WriteIndented = true,

        PropertyNameCaseInsensitive = true

    };

    [ObservableProperty]

    private CvxdGlobalConfig _globalConfig = new();



    [ObservableProperty]

    private ObservableCollection<PlaceholderCatalogItem> _systemPlaceholders = [];



    [ObservableProperty]

    private ObservableCollection<string> _placeholderScopes = ["CVXD", "VTĐV", "Cả 2"];



    [ObservableProperty]

    private ObservableCollection<string> _mappedPlaceholderScopes = ["CVXD", "VTĐV"];



    [ObservableProperty]

    private ObservableCollection<string> _placeholderValueSources = [];



    private readonly Stack<IUndoableAction> _undoStack = new();

    private readonly Stack<IUndoableAction> _redoStack = new();

    private bool _isRecordingAction = true;

    private bool _isUpdatingPlaceholderScope;

    private bool _isNormalizingSystemPlaceholderKey;

    private readonly Dictionary<object, Dictionary<string, object?>> _pendingOldValues = [];



    private readonly string _dataFolder;

    private readonly string _globalConfigPath;



    private FileSystemWatcher? _checklistWatcher;

    private FileSystemWatcher? _wordTemplateWatcher;

    private FileSystemWatcher? _templateProfilesWatcher;



    public CvxdWindowViewModel(MainViewModel mainVM, IAuthService authService)

    {

        _authService = authService;

        MainVM = mainVM;

        Documents = [];

        

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        _dataFolder = Path.Combine(appData, "DocumentManagement", "CVXD");

        _globalConfigPath = Path.Combine(_dataFolder, "global_config.json");

        Directory.CreateDirectory(_dataFolder);

        

        LoadGlobalConfig();
        AttachCvxdInfo(CvxdInfo);
        SharedProjectInfoTest.DataChanged = () => IsDirty = true;
        SharedProjectInfoTest.NotificationRequested = message => ShowToast(message);
        SharedProjectInfoTest.LoadFromLegacy(CvxdInfo);
        SharedProjectInfoTest.IsAdmin = IsAdmin;

        SystemPlaceholders = new ObservableCollection<PlaceholderCatalogItem>(PlaceholderRegistry.GetSystemPlaceholders());

        LoadChecklistTemplates();

        LoadNtTypes();

 

        GlobalConfig.PropertyChanged += (s, e) =>

        {

            if (e.PropertyName == nameof(GlobalConfig.TemplateFolderPath))

            {

                RefreshTemplateProfiles();
                UpdateTemplateProfilesWatcher();
                OnPropertyChanged(nameof(SelectedTemplatePath));
                OnPropertyChanged(nameof(SelectedChecklistPath));
                OnPropertyChanged(nameof(SelectedHsplTemplatePath));
                OnPropertyChanged(nameof(SelectedOtherTemplatePath));

                LoadNtTypes();

                LoadOtherTemplates();

                UpdateWordTemplateWatcher();

            }

            else if (e.PropertyName == nameof(GlobalConfig.ChecklistFolderPath))

            {

                RefreshTemplateProfiles();
                UpdateTemplateProfilesWatcher();
                OnPropertyChanged(nameof(SelectedTemplatePath));
                OnPropertyChanged(nameof(SelectedChecklistPath));
                OnPropertyChanged(nameof(SelectedHsplTemplatePath));
                OnPropertyChanged(nameof(SelectedOtherTemplatePath));

                LoadChecklistTemplates();

                UpdateChecklistWatcher();

            }

        };



        CvxdItems.CollectionChanged += (s, e) =>

        {

            if (e.NewItems != null)

            {

                foreach (CvxdItem item in e.NewItems)

                {

                    item.PropertyChanged += OnItemPropertyChanged;

                }

            }

            ValidateAllItems();

        };



        SetupWatchers();



        // Khởi tạo VtdvViewModel ngay trong constructor để nó không bao giờ null

        var exportService = new VtdvExportService();

        VtdvViewModel = new VtdvViewModel(MainVM, exportService, this);
        HsplViewModel = new HsplViewModel(
            MainVM,
            new HsplExportService(),
            () => TemplateProfileResolver.GetHsplRoot(GlobalConfig, CvxdInfo?.TemplateProfile),
            () => CvxdInfo);



        // Theo dõi thay đổi hợp đồng từ MainViewModel để đồng bộ toàn bộ cửa sổ

        MainVM.PropertyChanged += (s, e) =>

        {

            if (e.PropertyName == nameof(MainViewModel.SelectedDocument))

            {

                // Khi đổi HĐ ở Dashboard, cập nhật ngay SelectedDocument của cửa sổ này

                SelectedDocument = MainVM.SelectedDocument;

                PendingSelectedDocument = SelectedDocument;
                if (HsplViewModel != null)
                    HsplViewModel.SelectedDocument = SelectedDocument;
                PdvtViewModel?.RefreshContext();

            }

        };

    }



    public void Initialize()

    {

        // Refresh documents from MainViewModel

        Documents.Clear();

        foreach (var doc in MainVM.Documents)

        {

            Documents.Add(doc);

        }

        

        LoadSelectedDocument();

        PendingSelectedDocument = SelectedDocument;



        // Initialize VtdvViewModel ngay để sẵn sàng khi user chuyển tab

        if (VtdvViewModel == null)

        {

            var exportService = new VtdvExportService();

            VtdvViewModel = new VtdvViewModel(MainVM, exportService, this);

        }

        else

        {

            VtdvViewModel.RefreshContext();

        }



        IsDirty = false;

    }



    partial void OnSelectedTabIndexChanged(int value)

    {

        // Đảm bảo VtdvViewModel luôn được sync khi chuyển sang tab VTĐV

        if (value == 1 && VtdvViewModel != null)

        {

            VtdvViewModel.RefreshContext();

        }

        if (value == 5 && HsplViewModel != null)
        {
            HsplViewModel.SelectedDocument = SelectedDocument;
        }

        if (value == 7)
        {
            if (PdvtViewModel == null)
                PdvtViewModel = new PdvtViewModel(MainVM, () => CvxdInfo);
            else
                PdvtViewModel.RefreshContext();
        }

        if (value == 2 || value == 6)
        {
            SharedProjectInfoTest.LoadFromLegacy(CvxdInfo);
        }

    }



    // Search functionality

    partial void OnSearchTextChanged(string value)

    {

        if (string.IsNullOrWhiteSpace(value))

        {

            // Show all items

            foreach (var item in CvxdItems)

            {

                item.IsVisible = true;

            }

        }

        else

        {

            // Filter items based on search text

            var searchLower = value.ToLower();

            foreach (var item in CvxdItems)

            {

                item.IsVisible = item.NoiDung?.ToLower().Contains(searchLower) == true ||

                                 item.LoaiNt?.ToLower().Contains(searchLower) == true ||

                                 item.LoaiBb?.ToLower().Contains(searchLower) == true ||

                                 item.TenNt?.ToLower().Contains(searchLower) == true;

            }

        }

    }



    private void SetupWatchers()

    {

        try

        {

            UpdateTemplateProfilesWatcher();
            UpdateChecklistWatcher();

            UpdateWordTemplateWatcher();

        }

        catch (Exception ex)

        {

            Debug.WriteLine($"Lỗi setup watchers: {ex.Message}");

        }

    }



    private void UpdateTemplateProfilesWatcher()
    {
        try
        {
            _templateProfilesWatcher?.Dispose();
            _templateProfilesWatcher = null;

            var root = TemplateProfileResolver.GetProfilesRoot(GlobalConfig);
            if (!Directory.Exists(root)) return;

            _templateProfilesWatcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };
            _templateProfilesWatcher.Created += (_, _) => ScheduleTemplateProfilesRefresh();
            _templateProfilesWatcher.Deleted += (_, _) => ScheduleTemplateProfilesRefresh();
            _templateProfilesWatcher.Renamed += (_, _) => ScheduleTemplateProfilesRefresh();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi theo dõi thư mục bộ template: {ex.Message}");
        }
    }

    private async void ScheduleTemplateProfilesRefresh()
    {
        try
        {
            await Task.Delay(300);
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null) return;
            await dispatcher.InvokeAsync(RefreshTemplateProfiles);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi cập nhật danh sách bộ template: {ex.Message}");
        }
    }

    private void UpdateChecklistWatcher()

    {

        try

        {

            string checklistPath = (!string.IsNullOrEmpty(GlobalConfig.ChecklistFolderPath) && Directory.Exists(GlobalConfig.ChecklistFolderPath))

                ? TemplateProfileResolver.GetChecklistRoot(GlobalConfig, CvxdInfo?.TemplateProfile)

                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Checklists");



            _checklistWatcher?.Dispose();
            _checklistWatcher = null;
            if (!Directory.Exists(checklistPath)) return;



            _checklistWatcher = new FileSystemWatcher(checklistPath, "*.xlsx")

            {

                EnableRaisingEvents = true,

                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite

            };

            _checklistWatcher.Created += (s, e) => System.Windows.Application.Current.Dispatcher.Invoke(LoadChecklistTemplates);

            _checklistWatcher.Deleted += (s, e) => System.Windows.Application.Current.Dispatcher.Invoke(LoadChecklistTemplates);

            _checklistWatcher.Renamed += (s, e) => System.Windows.Application.Current.Dispatcher.Invoke(LoadChecklistTemplates);

        }

        catch (Exception ex)

        {

            Debug.WriteLine($"Lỗi update checklist watcher: {ex.Message}");

        }

    }



    private void UpdateWordTemplateWatcher()

    {

        try

        {

            string parentRoot = TemplateProfileResolver.GetTemplateRoot(GlobalConfig, CvxdInfo?.TemplateProfile);



            string cvxdFolder = Path.Combine(parentRoot, "CVXD");



            _wordTemplateWatcher?.Dispose();

            if (!Directory.Exists(cvxdFolder))
            {
                _wordTemplateWatcher = null;
                return;
            }

            _wordTemplateWatcher = new FileSystemWatcher(cvxdFolder, "*.docx")

            {

                EnableRaisingEvents = true,

                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite

            };

            _wordTemplateWatcher.Created += (s, e) => System.Windows.Application.Current.Dispatcher.Invoke(LoadNtTypes);

            _wordTemplateWatcher.Deleted += (s, e) => System.Windows.Application.Current.Dispatcher.Invoke(LoadNtTypes);

            _wordTemplateWatcher.Renamed += (s, e) => System.Windows.Application.Current.Dispatcher.Invoke(LoadNtTypes);

        }

        catch (Exception ex)

        {

            Debug.WriteLine($"Lỗi update word template watcher: {ex.Message}");

        }

    }



    private void LoadGlobalConfig()

    {

        try

        {

            if (File.Exists(_globalConfigPath))

            {

                string json = File.ReadAllText(_globalConfigPath);

                var config = JsonSerializer.Deserialize<CvxdGlobalConfig>(json, _jsonOptions);

                if (config != null)

                {

                    GlobalConfig = config;

                    RemoveSystemPlaceholders(GlobalConfig.GlobalPlaceholders);

                }

            }

            SeedDefaultStandardsIfNeeded();

        }

        catch (Exception ex)

        {

            Debug.WriteLine($"Error loading global config: {ex.Message}");

        }

    }



    [RelayCommand]

    private void SaveGlobalConfig()

    {

        if (SaveGlobalConfigInternal())

        {

            CvxdMessageBox.Show("Thành công", "Đã lưu Tiêu chuẩn và Placeholder vào cấu hình dùng chung toàn hệ thống!", MessageBoxButton.OK, MessageBoxImage.Information);

        }

        else

        {

            CvxdMessageBox.Show("Lỗi", "Lỗi khi lưu cấu hình dùng chung.", MessageBoxButton.OK, MessageBoxImage.Error);

        }

    }



    private bool SaveGlobalConfigInternal()

    {

        try

        {

            // Đồng bộ Placeholder dùng chung từ danh sách hiện tại

            RemoveSystemPlaceholders(GlobalConfig.GlobalPlaceholders);



            string json = JsonSerializer.Serialize(GlobalConfig, _jsonOptions);

            File.WriteAllText(_globalConfigPath, json);

            return true;

        }

        catch (Exception ex)

        {

            Debug.WriteLine($"Error saving global config: {ex.Message}");

            return false;

        }

    }



    [RelayCommand]

    private void RestoreDefaultPlaceholders()

    {

        try

        {

            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "cvxd_placeholder_defaults.json");

            if (File.Exists(configPath))

            {

                var json = File.ReadAllText(configPath);

                var defaults = JsonSerializer.Deserialize<List<CvxdCustomPlaceholder>>(json, _jsonOptions);

                if (defaults != null)

                {

                    CvxdInfo.CustomPlaceholders = new ObservableCollection<CvxdCustomPlaceholder>(

                        defaults.Where(p => !PlaceholderRegistry.IsSystemPlaceholder(p.Key)));

                    IsDirty = true;

                    MessageBox.Show("Đã khôi phục danh sách Placeholder về mặc định của hệ thống!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                }

            }

        }

        catch (Exception ex)

        {

            MessageBox.Show($"Lỗi khi khôi phục mặc định: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);

        }

    }



    [RelayCommand]

    private void PopulateStandardPlaceholders()

    {

        try

        {

            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "cvxd_placeholder_defaults.json");

            if (File.Exists(configPath))

            {

                var json = File.ReadAllText(configPath);

                var defaults = JsonSerializer.Deserialize<List<CvxdCustomPlaceholder>>(json, _jsonOptions);

                if (defaults != null)

                {

                    foreach (var item in defaults)

                    {

                        if (!PlaceholderRegistry.IsSystemPlaceholder(item.Key) &&

                            !CvxdInfo.CustomPlaceholders.Any(p => p.Key == item.Key))

                        {

                            CvxdInfo.CustomPlaceholders.Add(new CvxdCustomPlaceholder { Key = item.Key, Value = item.Value });

                        }

                    }

                    IsDirty = true;

                    MessageBox.Show("Đã nạp các mã Placeholder tiêu chuẩn vào danh sách!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                }

            }

        }

        catch (Exception ex)

        {

            MessageBox.Show($"Lỗi khi nạp mã chuẩn: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);

        }

    }



    private void LoadSelectedDocument()

    {

        if (MainVM.SelectedDocument != null)

        {

            SelectedDocument = MainVM.SelectedDocument;

        }

    }



    private void RefreshTemplateProfiles()
    {
        var profiles = TemplateProfileResolver.GetProfiles(GlobalConfig);
        CvxdInfo.TemplateProfiles.Clear();
        foreach (var profile in profiles) CvxdInfo.TemplateProfiles.Add(profile);

        if (string.IsNullOrWhiteSpace(CvxdInfo.TemplateProfile) && profiles.Count > 0)
        {
            CvxdInfo.TemplateProfile = profiles.FirstOrDefault(p => p.Equals("Tổng thầu", StringComparison.OrdinalIgnoreCase))
                ?? profiles[0];
        }
    }

    private void SeedDefaultStandardsIfNeeded()
    {
        if (GlobalConfig.StandardsSeedVersion >= DefaultCvxdStandards.SeedVersion)
            return;

        if (GlobalConfig.GlobalStandards.Count == 0)
        {
            foreach (var standard in DefaultCvxdStandards.Create())
                GlobalConfig.GlobalStandards.Add(standard);
        }

        // Mark the seed as applied so an intentional user deletion is preserved.
        GlobalConfig.StandardsSeedVersion = DefaultCvxdStandards.SeedVersion;
        SaveGlobalConfigInternal();
    }

    private void AttachCvxdInfo(CvxdInfo info)
    {
        info.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(CvxdInfo.TemplateProfile)) return;
            OnPropertyChanged(nameof(SelectedTemplatePath));
            OnPropertyChanged(nameof(SelectedChecklistPath));
            OnPropertyChanged(nameof(SelectedHsplTemplatePath));
            OnPropertyChanged(nameof(SelectedOtherTemplatePath));
            LoadNtTypes();
            LoadChecklistTemplates();
            LoadOtherTemplates();
            HsplViewModel?.RefreshTemplates();
            UpdateWordTemplateWatcher();
            UpdateChecklistWatcher();
        };
        RefreshTemplateProfiles();
    }

    partial void OnCvxdInfoChanged(CvxdInfo value)
    {
        AttachCvxdInfo(value);
        SharedProjectInfoTest.LoadFromLegacy(value);
        OnPropertyChanged(nameof(SelectedTemplatePath));
        OnPropertyChanged(nameof(SelectedChecklistPath));
        OnPropertyChanged(nameof(SelectedHsplTemplatePath));
        OnPropertyChanged(nameof(SelectedOtherTemplatePath));
        LoadNtTypes();
        LoadChecklistTemplates();

        LoadOtherTemplates();
        HsplViewModel?.RefreshTemplates();
        UpdateWordTemplateWatcher();
        UpdateChecklistWatcher();
    }

    partial void OnOtherTemplateSearchChanged(string value)
    {
        OtherTemplatesView?.Refresh();
    }

    partial void OnShowSelectedOtherTemplatesOnlyChanged(bool value)
    {
        OtherTemplatesView?.Refresh();
    }

    public void SetOtherTemplateSelectedFilter(bool selectedOnly)
    {
        ShowSelectedOtherTemplatesOnly = selectedOnly;
    }

    public void SetOtherTemplateSort(bool descending)
    {
        if (OtherTemplatesView == null) return;

        using (OtherTemplatesView.DeferRefresh())
        {
            OtherTemplatesView.SortDescriptions.Clear();
            OtherTemplatesView.SortDescriptions.Add(new SortDescription(nameof(OtherTemplateItem.Name), descending ? ListSortDirection.Descending : ListSortDirection.Ascending));
        }
    }

    private bool FilterOtherTemplate(object item)
    {
        if (item is not OtherTemplateItem template) return false;

        if (ShowSelectedOtherTemplatesOnly && !SelectedOtherTemplates.Contains(template))
            return false;

        if (string.IsNullOrWhiteSpace(OtherTemplateSearch)) return true;
        return template.Name.Contains(OtherTemplateSearch, StringComparison.CurrentCultureIgnoreCase)
            || template.FullName.Contains(OtherTemplateSearch, StringComparison.CurrentCultureIgnoreCase)
            || template.Description.Contains(OtherTemplateSearch, StringComparison.CurrentCultureIgnoreCase);
    }

    private string GetOtherTemplateDescriptionsPath()
    {
        return Path.Combine(SelectedOtherTemplatePath, "hoso-khac-descriptions.json");
    }

    private Dictionary<string, string> LoadOtherTemplateDescriptions()
    {
        try
        {
            var path = GetOtherTemplateDescriptionsPath();
            if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json, _jsonOptions)
                       ?? new Dictionary<string, string>();
            return new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi đọc mô tả hồ sơ khác: {ex.Message}");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void SaveOtherTemplateDescriptions()
    {
        try
        {
            var folder = SelectedOtherTemplatePath;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var data = AvailableOtherTemplates
                .Where(item => !string.IsNullOrWhiteSpace(item.Description))
                .ToDictionary(
                    item => item.FullName,
                    item => item.Description.Trim(),
                    StringComparer.OrdinalIgnoreCase);

            File.WriteAllText(GetOtherTemplateDescriptionsPath(), JsonSerializer.Serialize(data, _jsonOptions));
            OtherTemplatesView?.Refresh();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi lưu mô tả hồ sơ khác: {ex.Message}");
        }
    }

    private void LoadChecklistTemplates()

    {

        try

        {

            string path = (!string.IsNullOrEmpty(GlobalConfig.ChecklistFolderPath) && Directory.Exists(GlobalConfig.ChecklistFolderPath))

                ? TemplateProfileResolver.GetChecklistRoot(GlobalConfig, CvxdInfo?.TemplateProfile)

                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Checklists");



            AvailableChecklists.Clear();
            if (!Directory.Exists(path)) return;

            var files = Directory.GetFiles(path, "*.xlsx");

            foreach (var file in files)

            {

                var fileName = Path.GetFileName(file);

                if (fileName.StartsWith("~$")) continue;



                var fileNameNoExt = Path.GetFileNameWithoutExtension(file);

                var separatorIndex = fileNameNoExt.IndexOf('_');
                var id = separatorIndex > 0 ? fileNameNoExt[..separatorIndex].Trim() : string.Empty;
                var name = separatorIndex > 0 ? fileNameNoExt[(separatorIndex + 1)..].Trim() : fileNameNoExt.Trim();

                if (!string.IsNullOrWhiteSpace(name))

                {

                    // Ghép các phần còn lại nếu có nhiều dấu gạch dưới, và Trim()

                    AvailableChecklists.Add(new ChecklistTemplateItem 

                    { 

                        Id = id, 

                        Name = name, 

                        FullName = fileNameNoExt,

                        FilePath = file

                    });

                }

            }

        }

        catch (Exception ex)

        {

            Debug.WriteLine($"Lỗi load checklist templates: {ex.Message}");

        }

    }



    private void SyncChecklistNamesForCurrentProfile()
    {
        if (CvxdItems.Count == 0 || AvailableChecklists.Count == 0) return;

        foreach (var item in CvxdItems)
        {
            var checklist = !string.IsNullOrWhiteSpace(item.ChecklistId)
                ? AvailableChecklists.FirstOrDefault(t => t.Id.Equals(item.ChecklistId, StringComparison.OrdinalIgnoreCase))
                : AvailableChecklists.FirstOrDefault(t => t.Name.Equals(item.LoaiBb, StringComparison.OrdinalIgnoreCase));

            if (checklist != null)
            {
                item.LoaiBb = checklist.Name;
                item.ChecklistId = checklist.Id;
            }
        }
    }

    private void LoadNtTypes()

    {

        try

        {

            // Danh sách các loại mặc định luôn phải có

            var types = new List<string> { "CVXD", "LĐTTB", "CL", "TA", "TK" };



            // Đường dẫn thư mục cha

            string parentRoot = (!string.IsNullOrEmpty(GlobalConfig.TemplateFolderPath) && Directory.Exists(GlobalConfig.TemplateFolderPath))

                ? TemplateProfileResolver.GetTemplateRoot(GlobalConfig, CvxdInfo?.TemplateProfile)

                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");



            // Đường dẫn thư mục con CVXD (module hiện tại)

            string cvxdFolder = Path.Combine(parentRoot, "CVXD");



            if (Directory.Exists(cvxdFolder))

            {

                var files = Directory.GetFiles(cvxdFolder, "*_Template.docx", SearchOption.TopDirectoryOnly);

                foreach (var file in files)

                {

                    string fullFileName = Path.GetFileName(file);

                    if (fullFileName.StartsWith("~$")) continue;



                    string fileName = Path.GetFileNameWithoutExtension(file);

                    // Tách lấy phần phía trước _Template

                    int index = fileName.IndexOf("_Template", StringComparison.OrdinalIgnoreCase);

                    if (index > 0)

                    {

                        string type = fileName[..index].Trim();

                        if (!types.Contains(type, StringComparer.OrdinalIgnoreCase))

                        {

                            types.Add(type);

                        }

                    }

                }

            }



            AvailableNtTypes = new ObservableCollection<string>(types.Distinct().OrderBy(t => t));

            LoadWordTemplates();

        }

        catch (Exception ex)

        {

            Debug.WriteLine($"Lỗi load NT types: {ex.Message}");

            // Fallback

            AvailableNtTypes = ["CVXD", "LĐTTB"];

            LoadWordTemplates();

        }

    }





    [RelayCommand]

    private void SelectTemplateFolder()

    {

        var dialog = new Microsoft.Win32.OpenFolderDialog

        {

            Title = "Chọn thư mục gốc TemplateRoot",

            InitialDirectory = (GlobalConfig.TemplateFolderPath != null && Directory.Exists(GlobalConfig.TemplateFolderPath))

                ? GlobalConfig.TemplateFolderPath 

                : AppDomain.CurrentDomain.BaseDirectory

        };



        if (dialog.ShowDialog() == true)

        {

            string selectedPath = dialog.FolderName;

            

            GlobalConfig.TemplateFolderPath = selectedPath;
            // Keep legacy fields synchronized so older saved configurations remain readable.
            GlobalConfig.ChecklistFolderPath = selectedPath;
            GlobalConfig.HsplTemplateFolderPath = selectedPath;

            OnPropertyChanged(nameof(GlobalConfig)); // Force UI refresh for nested property

            

            // Tự động lưu vào file config ngay lập tức

            bool saved = SaveGlobalConfigInternal();

            LoadWordTemplates();

            string saveStatus = saved

                ? "Đã lưu vào config tự động"

                : "Lỗi khi lưu config";

            CvxdMessageBox.Show("Cập nhật thành công",

                $"{saveStatus}\n\nTemplateRoot: {selectedPath}\n\nĐã nạp {AvailableWordTemplates.Count} file Word template.",

                MessageBoxButton.OK, MessageBoxImage.Information);

            

            UpdateWordTemplateWatcher();

        }

    }

    [RelayCommand]
    private void OpenHsplTemplateFolder()
    {
        try
        {
            var path = SelectedHsplTemplatePath;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "open" });
        }
        catch (Exception ex)
        {
            CvxdMessageBox.Show("Lỗi", $"Không thể mở thư mục HSPL: {ex.Message}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void SelectHsplTemplateFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Chọn thư mục gốc template Hồ sơ pháp lý",
            InitialDirectory = !string.IsNullOrWhiteSpace(GlobalConfig.HsplTemplateFolderPath) && Directory.Exists(GlobalConfig.HsplTemplateFolderPath)
                ? GlobalConfig.HsplTemplateFolderPath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "HSPL_templates")
        };

        if (dialog.ShowDialog() != true) return;
        GlobalConfig.HsplTemplateFolderPath = dialog.FolderName;
        SaveGlobalConfigInternal();
        HsplViewModel?.RefreshTemplates();
        CvxdMessageBox.Show("Thành công", $"Đã cập nhật thư mục HSPL:\n{dialog.FolderName}", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void OpenWordTemplateFolder()
    {
        try
        {
            string path = SelectedTemplatePath;

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            CvxdMessageBox.Show("Lỗi", $"Không thể mở thư mục: {ex.Message}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]

    private void SelectChecklistFolder()

    {

        var dialog = new Microsoft.Win32.OpenFolderDialog

        {

            Title = "Chọn thư mục chứa Checklist Templates (.xlsx)",

            InitialDirectory = (GlobalConfig.ChecklistFolderPath != null && Directory.Exists(GlobalConfig.ChecklistFolderPath))

                ? GlobalConfig.ChecklistFolderPath 

                : AppDomain.CurrentDomain.BaseDirectory

        };



        if (dialog.ShowDialog() == true)

        {

            GlobalConfig.ChecklistFolderPath = dialog.FolderName;

            OnPropertyChanged(nameof(GlobalConfig)); // Force UI refresh for nested property

            SaveGlobalConfigInternal(); // Tự động lưu không hiện thông báo

            

            CvxdMessageBox.Show("Thành công", $"Đã cập nhật thư mục Checklist:\n{dialog.FolderName}", 

                MessageBoxButton.OK, MessageBoxImage.Information);

        }

    }



    /// <summary>

    /// Kiểm tra logic ngày tháng của toàn bộ danh sách

    /// </summary>

    private void ValidateAllItems()

    {

        CvxdItem? previousItem = null;

        foreach (var item in CvxdItems)

        {

            ValidateItem(item, previousItem);

            previousItem = item;

        }

    }



    private static void ValidateItem(CvxdItem item, CvxdItem? previousItem)

    {

        bool hasWarning = false;

        string message = "";



        // 1. Kiểm tra trong cùng 1 dòng (Giờ Bắt đầu vs Kết thúc)

        DateTime? start = ParseTime(item.BatDau);

        DateTime? end = ParseTime(item.KetThuc);

        

        if (start != null && end != null && end < start)

        {

            hasWarning = true;

            message = "⚠️ Giờ kết thúc không thể trước giờ bắt đầu.";

        }



        // 2. Kiểm tra tính tuần tự (Dòng hiện tại vs Dòng trước)

        if (previousItem != null)

        {

            DateTime? prevDate = ParseDate(previousItem.NgayNt);

            DateTime? currDate = ParseDate(item.NgayNt);



            if (prevDate != null && currDate != null)

            {

                if (currDate < prevDate)

                {

                    hasWarning = true;

                    string seqMsg = "⚠️ Ngày nghiệm thu không đúng trình tự (trước ngày của biên bản phía trên).";

                    message = string.IsNullOrEmpty(message) ? seqMsg : $"{message}\n{seqMsg}";

                }

            }

        }



        item.HasWarning = hasWarning;

        item.WarningMessage = message;

    }



    private static DateTime? ParseDate(string dateStr)

    {

        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        if (DateTime.TryParseExact(dateStr.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))

            return date;

        return null;

    }



    private static DateTime? ParseTime(string timeStr)

    {

        if (string.IsNullOrWhiteSpace(timeStr)) return null;

        

        // Hỗ trợ format: 9h, 9h30, 09:30, 14, 14:00

        var match = TimeRegex().Match(timeStr.Trim());

        if (match.Success)

        {

            if (int.TryParse(match.Groups[1].Value, out int hour))

            {

                int minute = 0;

                if (match.Groups[2].Success && !string.IsNullOrEmpty(match.Groups[2].Value))

                {

                    _ = int.TryParse(match.Groups[2].Value, out minute);

                }



                if (hour >= 0 && hour < 24 && minute >= 0 && minute < 60)

                {

                    return new DateTime(2000, 1, 1, hour, minute, 0); // Trả về ngày giả định để so sánh giờ

                }

            }

        }

        return null;

    }



    /// <summary>



    /// Khi chọn Hợp đồng khác → tự động load dữ liệu CVXD từ JSON (nếu có).

    /// </summary>

    partial void OnSelectedDocumentChanged(Document? value)

    {

        if (PendingSelectedDocument != value)

        {

            PendingSelectedDocument = value;

        }



        if (value != null)

        {

            LoadCvxdData(value);

            // Reset state sau khi load mới

            IsDirty = false;

            AttachChangeTracking();



            // Đảm bảo VTĐV cũng được làm mới khi đổi hợp đồng

            VtdvViewModel?.RefreshContext(value);

            IsDirty = false;

        }

        else

        {

            CvxdItems.Clear();

            CvxdInfo = new CvxdInfo();

            IsDirty = false;

        }

    }



    [RelayCommand]

    private void ApplyPendingSelectedDocument()

    {

        if (PendingSelectedDocument == null)

        {

            CvxdMessageBox.Show("Chọn hợp đồng", "Vui lòng chọn một hợp đồng trước khi tải dữ liệu.", MessageBoxButton.OK, MessageBoxImage.Information);

            return;

        }



        if (SelectedDocument == PendingSelectedDocument)

        {

            return;

        }



        SelectedDocument = PendingSelectedDocument;

        ShowToast($"Đã chọn HĐ: {SelectedDocument.ContractNumber}");

    }



    private async void ShowToast(string message)

    {

        ToastMessage = message;

        IsToastVisible = true;

        await Task.Delay(2200);

        if (ToastMessage == message)

        {

            IsToastVisible = false;

        }

    }



    private void AttachChangeTracking()

    {

        // Theo dõi thay đổi thông tin chung

        // Gỡ bỏ handler cũ nếu có để tránh tích tụ

        CvxdInfo.PropertyChanged -= OnCvxdInfoPropertyChanged;

        CvxdInfo.PropertyChanged += OnCvxdInfoPropertyChanged;

        RegisterUndoRedo(CvxdInfo);

        

        // Theo dõi danh sách biên bản

        CvxdItems.CollectionChanged -= OnCvxdItemsCollectionChanged;

        CvxdItems.CollectionChanged += OnCvxdItemsCollectionChanged;



        foreach (var item in CvxdItems)

        {

            item.PropertyChanged -= OnItemPropertyChangedForDirty;

            item.PropertyChanged += OnItemPropertyChangedForDirty;

            RegisterUndoRedo(item);

        }



        // Theo dõi cấu hình tiêu chuẩn và placeholder

        GlobalConfig.GlobalStandards.CollectionChanged += (s, e) => IsDirty = true;

        GlobalConfig.GlobalPlaceholders.CollectionChanged += (s, e) =>

        {

            IsDirty = true;

            TrackPlaceholderCollection(GlobalConfig.GlobalPlaceholders);

            UpdatePlaceholderPreviews();

        };

        TrackPlaceholderCollection(GlobalConfig.GlobalPlaceholders);

        TrackPlaceholderCollection(CvxdInfo.CustomPlaceholders);

        CvxdInfo.CustomPlaceholders.CollectionChanged += (s, e) =>

        {

            IsDirty = true;

            TrackPlaceholderCollection(CvxdInfo.CustomPlaceholders);

            UpdatePlaceholderPreviews();

        };

        UpdatePlaceholderPreviews();

    }



    partial void OnSelectedCvxdItemChanged(CvxdItem? value)

    {

        UpdatePlaceholderPreviews();

    }



    private void RefreshPlaceholderValueSources()

    {

        var sources = new List<string>();

        sources.AddRange(CvxdInfo.CustomColumns

            .Select(c => c.Name?.Trim() ?? string.Empty)

            .Where(name => !string.IsNullOrWhiteSpace(name)));

        sources.AddRange(CvxdInfo.VtdvCustomColumns

            .Select(c => c.Name?.Trim() ?? string.Empty)

            .Where(name => !string.IsNullOrWhiteSpace(name)));



        PlaceholderValueSources = new ObservableCollection<string>(

            sources.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x));

    }



    public void RefreshPlaceholderConfiguration()

    {

        UpdatePlaceholderPreviews();

    }



    private void TrackPlaceholderCollection(ObservableCollection<CvxdCustomPlaceholder>? placeholders)

    {

        if (placeholders == null) return;



        foreach (var placeholder in placeholders)

        {

            placeholder.PropertyChanged -= OnPlaceholderPropertyChanged;

            placeholder.PropertyChanged += OnPlaceholderPropertyChanged;

        }

    }



    private void OnPlaceholderPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)

    {

        if (e.PropertyName == nameof(CvxdCustomPlaceholder.Key) ||

            e.PropertyName == nameof(CvxdCustomPlaceholder.Value) ||

            e.PropertyName == nameof(CvxdCustomPlaceholder.Scope))

        {

            if (!_isNormalizingSystemPlaceholderKey &&

                sender is CvxdCustomPlaceholder keyPlaceholder &&

                e.PropertyName == nameof(CvxdCustomPlaceholder.Key) &&

                PlaceholderRegistry.IsSystemPlaceholder(keyPlaceholder.Key))

            {

                var blockedKey = keyPlaceholder.Key;

                try

                {

                    _isNormalizingSystemPlaceholderKey = true;

                    keyPlaceholder.Key = string.Empty;

                }

                finally

                {

                    _isNormalizingSystemPlaceholderKey = false;

                }



                CvxdMessageBox.Show(

                    "Placeholder hệ thống",

                    $"'{blockedKey}' là placeholder hệ thống của app, không thể khai báo lại ở Placeholder cố định/không cố định.",

                    MessageBoxButton.OK,

                    MessageBoxImage.Warning);

            }



            if (sender is CvxdCustomPlaceholder placeholder && e.PropertyName == nameof(CvxdCustomPlaceholder.Value))

            {

                ApplyMappedPlaceholderScope(placeholder);

            }



            UpdatePlaceholderPreviews();

            IsDirty = true;

        }

    }



    private void UpdatePlaceholderPreviews()

    {

        var doc = SelectedDocument ?? MainVM.SelectedDocument;

        if (doc == null || CvxdInfo == null) return;



        var context = SelectedCvxdItem ?? CvxdItems.FirstOrDefault();

        RefreshPlaceholderValueSources();

        NormalizeMappedPlaceholderScopes();

        UpdatePlaceholderPreviews(GlobalConfig.GlobalPlaceholders, doc, context);

        UpdatePlaceholderPreviews(CvxdInfo.CustomPlaceholders, doc, context);

    }



    private void NormalizeMappedPlaceholderScopes()

    {

        if (CvxdInfo?.CustomPlaceholders == null) return;

        foreach (var placeholder in CvxdInfo.CustomPlaceholders)

        {

            ApplyMappedPlaceholderScope(placeholder);

        }

    }



    private void ApplyMappedPlaceholderScope(CvxdCustomPlaceholder placeholder)

    {

        if (_isUpdatingPlaceholderScope) return;



        var scope = InferMappedPlaceholderScope(placeholder.Value, placeholder.Scope);

        if (string.IsNullOrWhiteSpace(scope) || string.Equals(placeholder.Scope, scope, StringComparison.OrdinalIgnoreCase)) return;



        try

        {

            _isUpdatingPlaceholderScope = true;

            placeholder.Scope = scope;

        }

        finally

        {

            _isUpdatingPlaceholderScope = false;

        }

    }



    private string InferMappedPlaceholderScope(string? valueSource, string? currentScope)

    {

        var source = valueSource?.Trim();

        if (string.IsNullOrWhiteSpace(source))

            return IsMappedScope(currentScope) ? currentScope! : "CVXD";



        var isCvxdColumn = CvxdInfo.CustomColumns.Any(c => string.Equals(c.Name?.Trim(), source, StringComparison.OrdinalIgnoreCase));

        var isVtdvColumn = CvxdInfo.VtdvCustomColumns.Any(c => string.Equals(c.Name?.Trim(), source, StringComparison.OrdinalIgnoreCase));



        if (isCvxdColumn && !isVtdvColumn) return "CVXD";

        if (isVtdvColumn && !isCvxdColumn) return "VTĐV";

        if (isCvxdColumn && isVtdvColumn && IsMappedScope(currentScope)) return currentScope!;



        return IsMappedScope(currentScope) ? currentScope! : "CVXD";

    }



    private static bool IsMappedScope(string? scope)

    {

        return scope?.Equals("CVXD", StringComparison.OrdinalIgnoreCase) == true ||

               scope?.Equals("VTĐV", StringComparison.OrdinalIgnoreCase) == true;

    }



    private void UpdatePlaceholderPreviews(ObservableCollection<CvxdCustomPlaceholder>? placeholders, Document doc, CvxdItem? context)

    {

        if (placeholders == null) return;



        foreach (var placeholder in placeholders)

        {

            var evaluation = PlaceholderRegistry.EvaluatePattern(placeholder.Value ?? string.Empty, doc, CvxdInfo, context);

            placeholder.Preview = evaluation.Value;

            placeholder.MissingTokens = string.Join(", ", evaluation.MissingTokens.Distinct(StringComparer.OrdinalIgnoreCase));

        }

    }



    private void RegisterUndoRedo(object obj)

    {

        if (obj is System.ComponentModel.INotifyPropertyChanging npc)

        {

            npc.PropertyChanging += (s, e) =>

            {

                if (!_isRecordingAction || string.IsNullOrEmpty(e.PropertyName)) return;

                

                var prop = s?.GetType().GetProperty(e.PropertyName);

                if (prop != null)

                {

                    if (!_pendingOldValues.ContainsKey(s!))

                        _pendingOldValues[s!] = [];

                    

                    try { _pendingOldValues[s!][e.PropertyName] = prop.GetValue(s); } catch { }

                }

            };

        }



        if (obj is System.ComponentModel.INotifyPropertyChanged np)

        {

            np.PropertyChanged += (s, e) =>

            {

                if (!_isRecordingAction || string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(IsDirty)) return;



                if (_pendingOldValues.TryGetValue(s!, out var props) && props.TryGetValue(e.PropertyName, out var oldValue))

                {

                    var prop = s?.GetType().GetProperty(e.PropertyName);

                    if (prop != null)

                    {

                        try 

                        {

                            var newValue = prop.GetValue(s);

                            if (!Equals(oldValue, newValue))

                            {

                                PushAction(new PropertyChangeAction(s!, e.PropertyName, oldValue, newValue, _ => IsDirty = true));

                            }

                        } 

                        catch { }

                    }

                    props.Remove(e.PropertyName);

                }

            };

        }

    }



    private void PushAction(IUndoableAction action)

    {

        if (!_isRecordingAction) return;

        _undoStack.Push(action);

        _redoStack.Clear();

        UndoCommand.NotifyCanExecuteChanged();

        RedoCommand.NotifyCanExecuteChanged();

    }



    public void RecordCellEditUndo(object target, string propertyName, object? oldValue, object? newValue)

    {

        if (string.IsNullOrWhiteSpace(propertyName)) return;

        if (Equals(oldValue, newValue)) return;



        PushAction(new PropertyChangeAction(target, propertyName, oldValue, newValue, _ => IsDirty = true));

    }

    public void RecordCellEditBatchUndo(IEnumerable<IUndoableAction> actions)
    {
        var batch = new CompositeUndoAction(actions);
        if (batch.HasActions) PushAction(batch);
    }



    [RelayCommand(CanExecute = nameof(CanUndoAdmin))]

    private void Undo()

    {

        if (_undoStack.Count == 0) return;

        _isRecordingAction = false;

        var action = _undoStack.Pop();

        action.Undo();

        _redoStack.Push(action);

        _isRecordingAction = true;

        

        UndoCommand.NotifyCanExecuteChanged();

        RedoCommand.NotifyCanExecuteChanged();

    }



    private bool CanUndoAdmin() => _authService.IsAdmin && _undoStack.Count > 0;



    [RelayCommand(CanExecute = nameof(CanRedoAdmin))]

    private void Redo()

    {

        if (_redoStack.Count == 0) return;

        _isRecordingAction = false;

        var action = _redoStack.Pop();

        action.Redo();

        _undoStack.Push(action);

        _isRecordingAction = true;



        UndoCommand.NotifyCanExecuteChanged();

        RedoCommand.NotifyCanExecuteChanged();

    }



    private bool CanRedoAdmin() => _authService.IsAdmin && _redoStack.Count > 0;



    private void OnCvxdInfoPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)

    {

        IsDirty = true;

    }



    private void OnCvxdItemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)

    {

        IsDirty = true;

        if (_isRecordingAction && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)

        {

            foreach (CvxdItem item in e.NewItems)

            {

                PushAction(new CollectionChangeAction<CvxdItem>(CvxdItems, item, e.NewStartingIndex, true));

            }

        }

        

        ReorderStt();

    }



    private void ReorderStt()

    {

        bool wasRecording = _isRecordingAction;

        _isRecordingAction = false;

        try

        {

            for (int i = 0; i < CvxdItems.Count; i++)

            {

                CvxdItems[i].Stt = (i + 1).ToString();

            }

        }

        finally

        {

            _isRecordingAction = wasRecording;

        }

    }



    private void OnItemPropertyChangedForDirty(object? sender, System.ComponentModel.PropertyChangedEventArgs e)

    {

        if (e.PropertyName != nameof(CvxdItem.HasError))

        {

            IsDirty = true;

        }

    }





    [RelayCommand]

    private void AddRow()

    {

        if (!_authService.IsAdmin) return;



        var newItem = new CvxdItem

        {

            Stt = (CvxdItems.Count + 1).ToString()

        };

        

        if (SelectedCvxdItem != null)

        {

            int index = CvxdItems.IndexOf(SelectedCvxdItem);

            if (index >= 0)

            {

                CvxdItems.Insert(index + 1, newItem);

                return;

            }

        }

        

        CvxdItems.Add(newItem);

        EnsureCustomFields(newItem);

    }

    public int CountFindReplaceMatches(string find, bool vtdvOnly)
    {
        if (string.IsNullOrEmpty(find)) return 0;
        return BuildFindReplaceFields(vtdvOnly).Sum(field => CountOccurrences(field.Get()));

        int CountOccurrences(string value)
        {
            var count = 0;
            var start = 0;
            while ((start = value.IndexOf(find, start, StringComparison.CurrentCultureIgnoreCase)) >= 0)
            {
                count++;
                start += find.Length;
            }
            return count;
        }
    }

    public int ReplaceAll(string find, string replacement, bool vtdvOnly)
    {
        if (string.IsNullOrEmpty(find)) return 0;
        var changed = 0;
        foreach (var field in BuildFindReplaceFields(vtdvOnly))
        {
            var value = field.Get();
            var updated = value.Replace(find, replacement ?? string.Empty, StringComparison.CurrentCultureIgnoreCase);
            if (updated == value) continue;
            field.Set(updated);
            changed += CountOccurrences(value);
        }

        if (changed > 0) IsDirty = true;
        return changed;

        int CountOccurrences(string value)
        {
            var count = 0;
            var start = 0;
            while ((start = value.IndexOf(find, start, StringComparison.CurrentCultureIgnoreCase)) >= 0)
            {
                count++;
                start += find.Length;
            }
            return count;
        }
    }

    public int ReplaceNext(string find, string replacement, bool vtdvOnly)
    {
        if (string.IsNullOrEmpty(find)) return 0;
        foreach (var field in BuildFindReplaceFields(vtdvOnly))
        {
            var value = field.Get();
            var index = value.IndexOf(find, StringComparison.CurrentCultureIgnoreCase);
            if (index < 0) continue;
            field.Set(value.Remove(index, find.Length).Insert(index, replacement ?? string.Empty));
            IsDirty = true;
            return 1;
        }
        return 0;
    }

    public CvxdItem? FindFirstCvxdItem(string find)
    {
        if (string.IsNullOrEmpty(find)) return null;
        return CvxdItems.FirstOrDefault(item =>
            new[] { item.Tcnt, item.LoaiNt, item.NoiDung, item.NgayNt, item.BatDau,
                    item.KetThuc, item.SoChecklist, item.SoPdvt, item.LoaiBb,
                    item.ChecklistId, item.TenNt }
                .Concat(item.CustomFields.Select(field => field.Value))
                .Any(value => value.Contains(find, StringComparison.CurrentCultureIgnoreCase)));
    }

    public CvxdItem? FindNextCvxdItem(string find, bool advance)
    {
        var matches = CvxdItems.Where(item =>
            new[] { item.Tcnt, item.LoaiNt, item.NoiDung, item.NgayNt, item.BatDau,
                    item.KetThuc, item.SoChecklist, item.SoPdvt, item.LoaiBb,
                    item.ChecklistId, item.TenNt }
                .Concat(item.CustomFields.Select(field => field.Value))
                .Any(value => value.Contains(find, StringComparison.CurrentCultureIgnoreCase))).ToList();
        if (matches.Count == 0) return null;
        if (!advance || SelectedCvxdItem == null) return matches[0];
        var index = matches.IndexOf(SelectedCvxdItem);
        return matches[(index + 1 + matches.Count) % matches.Count];
    }

    private IEnumerable<(Func<string> Get, Action<string> Set)> BuildFindReplaceFields(bool vtdvOnly)
    {
        if (!vtdvOnly)
        {
            foreach (var item in CvxdItems)
            {
                yield return (() => item.Tcnt, value => item.Tcnt = value);
                yield return (() => item.LoaiNt, value => item.LoaiNt = value);
                yield return (() => item.NoiDung, value => item.NoiDung = value);
                yield return (() => item.NgayNt, value => item.NgayNt = value);
                yield return (() => item.BatDau, value => item.BatDau = value);
                yield return (() => item.KetThuc, value => item.KetThuc = value);
                yield return (() => item.SoChecklist, value => item.SoChecklist = value);
                yield return (() => item.SoPdvt, value => item.SoPdvt = value);
                yield return (() => item.LoaiBb, value => item.LoaiBb = value);
                yield return (() => item.ChecklistId, value => item.ChecklistId = value);
                yield return (() => item.TenNt, value => item.TenNt = value);
                foreach (var field in item.CustomFields)
                    yield return (() => field.Value, value => field.Value = value);
            }
        }

        if (vtdvOnly)
        {
            var selectedSession = VtdvViewModel?.SelectedSession;
            if (selectedSession == null) yield break;
            foreach (var session in new[] { selectedSession })
            foreach (var item in session.Items)
            {
                yield return (() => item.DoiTuong, value => item.DoiTuong = value);
                yield return (() => item.SoPhieuXuat, value => item.SoPhieuXuat = value);
                yield return (() => item.SoLuong, value => item.SoLuong = value);
                yield return (() => item.DonVi, value => item.DonVi = value);
                yield return (() => item.ThuongHieu, value => item.ThuongHieu = value);
                yield return (() => item.MaSanPham, value => item.MaSanPham = value);
                yield return (() => item.ChatLuong, value => item.ChatLuong = value);
                yield return (() => item.GhiChu, value => item.GhiChu = value);
                foreach (var field in item.CustomFields)
                    yield return (() => field.Value, value => field.Value = value);
            }
        }
    }



    private void EnsureCustomFields(CvxdItem item)

    {

        foreach (var column in CvxdInfo.CustomColumns)

        {

            if (string.IsNullOrWhiteSpace(column.Name)) continue;

            if (!item.CustomFields.Any(f => string.Equals(f.Name, column.Name, StringComparison.OrdinalIgnoreCase)))

            {

                item.CustomFields.Add(new CvxdCustomFieldValue { Name = column.Name });

            }

        }

    }



    [RelayCommand]

    private void AddCvxdCustomColumn()

    {

        if (!_authService.IsAdmin) return;



        var defaultName = $"CotMoi{CvxdInfo.CustomColumns.Count + 1}";

        var name = ColumnNameDialog.Prompt(

            "Thêm cột CVXD",

            "Nhập tên cột mới. Tên này sẽ xuất hiện trong bảng CVXD và dùng để mapping placeholder không cố định.",

            defaultName)?.Trim();

        if (string.IsNullOrWhiteSpace(name)) return;



        if (CvxdInfo.CustomColumns.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))

        {

            CvxdMessageBox.Show("Trùng tên cột", $"Cột '{name}' đã tồn tại.", MessageBoxButton.OK, MessageBoxImage.Warning);

            return;

        }



        CvxdInfo.CustomColumns.Add(new CvxdCustomColumn { Name = name });

        foreach (var item in CvxdItems)

        {

            EnsureCustomFields(item);

        }



        if (!CvxdInfo.CustomPlaceholders.Any(p => string.Equals(p.Value, name, StringComparison.OrdinalIgnoreCase)))

        {

            CvxdInfo.CustomPlaceholders.Add(new CvxdCustomPlaceholder

            {

                Key = "{" + name + "}",

                Value = name,

                Scope = "CVXD"

            });

        }



        IsDirty = true;

        RefreshPlaceholderValueSources();

        OnPropertyChanged(nameof(CvxdInfo));

    }



    [RelayCommand]

    private void DeleteCvxdCustomColumn(string? name)

    {

        if (!_authService.IsAdmin || string.IsNullOrWhiteSpace(name)) return;



        var column = CvxdInfo.CustomColumns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        if (column == null) return;



        var result = CvxdMessageBox.Show("Xóa cột", $"Xóa cột '{name}' và dữ liệu tương ứng trong các dòng CVXD?", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;



        CvxdInfo.CustomColumns.Remove(column);

        foreach (var item in CvxdItems)

        {

            for (int i = item.CustomFields.Count - 1; i >= 0; i--)

            {

                if (string.Equals(item.CustomFields[i].Name, name, StringComparison.OrdinalIgnoreCase))

                {

                    item.CustomFields.RemoveAt(i);

                }

            }

        }



        for (int i = CvxdInfo.CustomPlaceholders.Count - 1; i >= 0; i--)

        {

            var placeholder = CvxdInfo.CustomPlaceholders[i];

            if (string.Equals(placeholder.Value, name, StringComparison.OrdinalIgnoreCase) &&

                string.Equals(placeholder.Scope, "CVXD", StringComparison.OrdinalIgnoreCase))

            {

                CvxdInfo.CustomPlaceholders.RemoveAt(i);

            }

        }



        IsDirty = true;

        RefreshPlaceholderValueSources();

        OnPropertyChanged(nameof(CvxdInfo));

    }



    [RelayCommand]

    private void RenameCvxdCustomColumn(string? name)

    {

        if (!_authService.IsAdmin || string.IsNullOrWhiteSpace(name)) return;



        var column = CvxdInfo.CustomColumns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        if (column == null) return;



        var newName = ColumnNameDialog.Prompt(

            "Đổi tên cột CVXD",

            $"Nhập tên mới cho cột '{name}'. Dữ liệu hiện có và placeholder mapping sẽ được giữ lại.",

            name)?.Trim();

        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, name, StringComparison.OrdinalIgnoreCase)) return;



        if (CvxdInfo.CustomColumns.Any(c => !ReferenceEquals(c, column) && string.Equals(c.Name, newName, StringComparison.OrdinalIgnoreCase)))

        {

            CvxdMessageBox.Show("Trùng tên cột", $"Cột '{newName}' đã tồn tại.", MessageBoxButton.OK, MessageBoxImage.Warning);

            return;

        }



        column.Name = newName;

        foreach (var item in CvxdItems)

        {

            foreach (var field in item.CustomFields.Where(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))

            {

                field.Name = newName;

            }

            EnsureCustomFields(item);

        }



        UpdateMappedPlaceholdersForColumnRename(name, newName, "CVXD");



        IsDirty = true;

        RefreshPlaceholderValueSources();

        OnPropertyChanged(nameof(CvxdInfo));

    }



    private void UpdateMappedPlaceholdersForColumnRename(string oldName, string newName, string scope)

    {

        foreach (var placeholder in CvxdInfo.CustomPlaceholders)

        {

            if (!string.Equals(placeholder.Value, oldName, StringComparison.OrdinalIgnoreCase)) continue;

            if (!string.Equals(placeholder.Scope, scope, StringComparison.OrdinalIgnoreCase)) continue;



            placeholder.Value = newName;

            if (string.Equals(placeholder.Key, "{" + oldName + "}", StringComparison.OrdinalIgnoreCase))

            {

                placeholder.Key = "{" + newName + "}";

            }

        }

    }



    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)

    {

        if (sender is not CvxdItem item) return;



        if (e.PropertyName == nameof(CvxdItem.LoaiNt))

        {

            // Nếu là loại TA hoặc TK, ép SoChecklist = 0

            if (item.LoaiNt == "TA" || item.LoaiNt == "TK")

            {

                item.SoChecklist = "0";

                item.LoaiBb = "";

                item.ChecklistId = "";

            }

        }



        if (e.PropertyName == nameof(CvxdItem.NgayNt) || 

            e.PropertyName == nameof(CvxdItem.BatDau) || 

            e.PropertyName == nameof(CvxdItem.KetThuc))

        {

            ValidateAllItems();

        }



        if (e.PropertyName == nameof(CvxdItem.IsSelected))

        {

            if (_isSyncingSelection) return;

            

            _isSyncingSelection = true;

            try

            {

                AllSelected = CvxdItems.All(i => i.IsSelected);

            }

            finally

            {

                _isSyncingSelection = false;

            }

        }



        if (e.PropertyName == nameof(CvxdItem.LoaiBb))

        {

            string? searchName = item.LoaiBb?.Trim();

            if (!string.IsNullOrEmpty(searchName))

            {

                var template = AvailableChecklists.FirstOrDefault(t => 

                    t.Name.Equals(searchName, StringComparison.OrdinalIgnoreCase));

                    

                if (template != null)

                {

                    item.ChecklistId = template.Id;

                    if (string.IsNullOrWhiteSpace(item.TenNt) || item.TenNt.StartsWith("Lắp ")) 

                    {

                        item.TenNt = "Lắp " + template.Name.ToLower();

                    }

                }

            }

        }



        // 1. Xử lý định dạng NGÀY (Excel-style)

        if (e.PropertyName == nameof(CvxdItem.NgayNt))

        {

            string? input = item.NgayNt?.Trim();

            if (!string.IsNullOrEmpty(input))

            {

                // Hỗ trợ cực nhiều định dạng: 3/4/26, 3/4, 3-4, 3.4, 03042026...

                string[] formats = 

                [

                    "d/M/yyyy", "d/M/yy", "d/M", 

                    "d-M-yyyy", "d-M-yy", "d-M", 

                    "d.M.yyyy", "d.M.yy", "d.M",

                    "ddMMyyyy", "ddMMyy", "dMyy", "dMyyyy"

                ];



                if (DateTime.TryParseExact(input, formats, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out DateTime parsedDate))

                {

                    // Nếu thiếu năm (ví dụ nhập 3/4), tự động lấy năm hiện tại

                    string formatted = parsedDate.ToString("dd/MM/yyyy");

                    if (item.NgayNt != formatted)

                    {

                        item.NgayNt = formatted;

                    }

                }

            }

        }



        // 2. Xử lý định dạng GIỜ (Excel-style: 9 -> 09h00, 930 -> 09h30)

        if (e.PropertyName == nameof(CvxdItem.BatDau) || e.PropertyName == nameof(CvxdItem.KetThuc))

        {

            string? input = (e.PropertyName == nameof(CvxdItem.BatDau) ? item.BatDau : item.KetThuc)?.Trim();

            if (!string.IsNullOrEmpty(input) && !input.Contains('h') && !input.Contains(':'))

            {

                string formatted = string.Empty;

                if (input.Length <= 2) // Ví dụ: 9 -> 09h00, 14 -> 14h00

                {

                    if (int.TryParse(input, out int h) && h >= 0 && h < 24)

                        formatted = $"{h:D2}h00";

                }

                else if (input.Length == 3) // Ví dụ: 930 -> 09h30

                {

                    if (int.TryParse(input[..1], out int h) && int.TryParse(input[1..], out int m) && h < 24 && m < 60)

                        formatted = $"{h:D2}h{m:D2}";

                }

                else if (input.Length == 4) // Ví dụ: 1430 -> 14h30

                {

                    if (int.TryParse(input[..2], out int h) && int.TryParse(input[2..], out int m) && h < 24 && m < 60)

                        formatted = $"{h:D2}h{m:D2}";

                }



                if (!string.IsNullOrEmpty(formatted))

                {

                    if (e.PropertyName == nameof(CvxdItem.BatDau)) item.BatDau = formatted;

                    else item.KetThuc = formatted;

                }

            }

        }



        // Tự động kiểm tra lại lỗi khi người dùng thay đổi dữ liệu (Tránh đệ quy vô hạn khi HasError thay đổi)

        if (item.IsSelected && e.PropertyName != nameof(CvxdItem.HasError) && e.PropertyName != nameof(CvxdItem.HasWarning))

        {

            ValidateItems();

        }

    }



    [RelayCommand]

    private void DeleteRow(object? parameter)

    {

        if (parameter is CvxdItem item)

        {

            int index = CvxdItems.IndexOf(item);

            if (index >= 0)

            {

                PushAction(new CollectionChangeAction<CvxdItem>(CvxdItems, item, index, false));

                CvxdItems.Remove(item);

            }

        }

    }



    [RelayCommand]

    private void AddStandard()

    {

        GlobalConfig.GlobalStandards.Add(new CvxdStandard());

    }



    [RelayCommand]

    private void DeleteStandard(CvxdStandard std)

    {

        if (std != null)

        {

            GlobalConfig.GlobalStandards.Remove(std);

        }

    }



    [RelayCommand]

    private void AddGlobalPlaceholder()

    {

        GlobalConfig.GlobalPlaceholders.Add(new CvxdCustomPlaceholder { Scope = "Cả 2" });

        TrackPlaceholderCollection(GlobalConfig.GlobalPlaceholders);

        UpdatePlaceholderPreviews();

    }



    [RelayCommand]

    private void DeleteGlobalPlaceholder(CvxdCustomPlaceholder cp)

    {

        if (cp != null)

        {

            GlobalConfig.GlobalPlaceholders.Remove(cp);

            UpdatePlaceholderPreviews();

        }

    }



    [RelayCommand]

    private void AddCustomPlaceholder()

    {

        CvxdInfo.CustomPlaceholders.Add(new CvxdCustomPlaceholder { Scope = "CVXD" });

        TrackPlaceholderCollection(CvxdInfo.CustomPlaceholders);

        UpdatePlaceholderPreviews();

    }



    [RelayCommand]

    private void DeleteCustomPlaceholder(CvxdCustomPlaceholder cp)

    {

        if (cp != null)

        {

            CvxdInfo.CustomPlaceholders.Remove(cp);

            UpdatePlaceholderPreviews();

        }

    }



    [RelayCommand]

    private void OpenStandardsWindow()

    {

        SelectedTabIndex = 2;

    }



    [RelayCommand]

    private void SaveData()

    {

        if (SelectedDocument == null)

        {

            CvxdMessageBox.Show("Thông báo", "Vui lòng chọn Hợp đồng trước khi lưu!",

                MessageBoxButton.OK, MessageBoxImage.Warning);

            return;

        }



        try

        {

            SaveCvxdData(SelectedDocument);

            VtdvViewModel?.SaveVtdvDataOnly();

            SaveGlobalConfigInternal(); // Lưu luôn cấu hình dùng chung (Tiêu chuẩn, v.v.)

            IsDirty = false;

            CvxdMessageBox.Show("Thành công", "Đã lưu toàn bộ dữ liệu CVXD, VTĐV và cấu hình dùng chung thành công!",

                MessageBoxButton.OK, MessageBoxImage.Information);

        }

        catch (Exception ex)

        {

            CvxdMessageBox.Show("Lỗi", $"Lỗi khi lưu dữ liệu: {ex.Message}",

                MessageBoxButton.OK, MessageBoxImage.Error);

        }

    }



    [RelayCommand]
    private async Task ImportCvxdExcel()
    {
        await ImportExcel(AcceptanceExcelImportTarget.Cvxd);
    }

    [RelayCommand]
    private async Task ImportVtdvExcel()
    {
        await ImportExcel(AcceptanceExcelImportTarget.Vtdv);
    }

    private async Task ImportExcel(AcceptanceExcelImportTarget target)

    {

        if (SelectedDocument == null)

        {

            CvxdMessageBox.Show("Chọn hợp đồng", "Vui lòng chọn một hợp đồng trước khi import Excel.",

                MessageBoxButton.OK, MessageBoxImage.Information);

            return;

        }

        try

        {
            var owner = Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive);

            var baseFolder = SelectedDocument.FolderPath ?? AppDomain.CurrentDomain.BaseDirectory;
            var importWindow = new ImportAcceptanceExcelWindow(baseFolder, target);
            if (owner != null)
            {
                importWindow.Owner = owner;
            }

            if (importWindow.ShowDialog() != true || string.IsNullOrWhiteSpace(importWindow.SelectedFilePath))
            {
                return;
            }

            var importService = new AcceptanceExcelImportService();
            IsBusy = true;
            ProgressValue = 0;
            StatusMessage = target == AcceptanceExcelImportTarget.Cvxd
                ? "Đang đọc dữ liệu CVXD từ Excel..."
                : "Đang đọc dữ liệu VTĐV từ Excel...";

            AcceptanceExcelImportResult imported;
            try
            {
                imported = await Task.Run(() => target == AcceptanceExcelImportTarget.Cvxd
                    ? importService.ImportCvxd(importWindow.SelectedFilePath)
                    : importService.ImportVtdv(importWindow.SelectedFilePath));
            }
            finally
            {
                IsBusy = false;
                ProgressValue = 0;
                StatusMessage = string.Empty;
            }



            if (!imported.HasAnyData)

            {

                CvxdMessageBox.Show("Không có dữ liệu", "Không tìm thấy sheet CVXD hoặc các sheet VTĐV dạng 01, 02, 03... hợp lệ trong file Excel.",

                    MessageBoxButton.OK, MessageBoxImage.Warning);

                return;

            }



            var importMode = ImportModeDialog.Show(owner, target);

            if (importMode is null)

            {

                return;

            }



            ApplyImportedWorkbook(imported, importMode.Value);



            var importedParts = new List<string>();

            if (imported.HasCvxdSheet) importedParts.Add($"CVXD ({CvxdItems.Count} dòng)");

            if (imported.HasVtdvSheet) importedParts.Add($"VTĐV ({VtdvViewModel?.Sessions.Count ?? 0} biên bản)");



            CvxdMessageBox.Show("Import thành công",

                $"Đã {(importMode == AcceptanceExcelImportMode.Append ? "nạp thêm" : "nạp đè")} dữ liệu từ Excel vào hợp đồng {SelectedDocument.ContractNumber}:\n- {string.Join("\n- ", importedParts)}\n\nBấm Lưu để ghi xuống file dữ liệu nếu bạn muốn giữ thay đổi.",

                MessageBoxButton.OK, MessageBoxImage.Information);

        }

        catch (Exception ex)

        {

            CvxdMessageBox.Show("Lỗi import Excel", ex.Message,

                MessageBoxButton.OK, MessageBoxImage.Error);

        }

    }



    [RelayCommand]

    private void OpenChecklistFolder()

    {

        var path = GlobalConfig?.ChecklistFolderPath;

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))

        {

            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Checklists");

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    }



    [RelayCommand]

    private async Task ExportWord()

    {

        if (IsBusy)
        {
            CvxdMessageBox.Show("Đang xuất hồ sơ", "Vui lòng chờ tiến trình xuất hiện tại hoàn tất.",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (SelectedDocument == null)
        {
            CvxdMessageBox.Show("Chưa chọn hồ sơ", "Vui lòng chọn một hồ sơ/hợp đồng trước khi xuất Word.",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CvxdItems.Count == 0)
        {
            CvxdMessageBox.Show("Chưa có dữ liệu", "Chưa có biên bản CVXD để xuất. Vui lòng nhập hoặc import dữ liệu trước.",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }



        // Bổ sung kiểm tra dữ liệu trước khi xuất

        if (!ValidateItems())

        {

            CvxdMessageBox.Show("Cảnh báo dữ liệu", "Vui lòng nhập đầy đủ thông tin cho các dòng đã chọn (những dòng có màu đỏ).\n\nLưu ý: Chỉ được bỏ trống cột 'Số PDVT'.", 

                MessageBoxButton.OK, MessageBoxImage.Warning);

            return;

        }



        var service = new CvxdExportService();

        var preflight = service.ValidateExport(
            GlobalConfig,
            [.. CvxdItems],
            exportAll: false,
            profile: CvxdInfo.TemplateProfile);

        if (!preflight.CanExport)

        {

            CvxdMessageBox.Show("Kiểm tra trước khi xuất", preflight.ToMessage(),

                MessageBoxButton.OK, MessageBoxImage.Warning);

            return;

        }



        IsBusy = true;

        StatusMessage = "Đang khởi tạo tiến trình xuất Word...";

        ProgressValue = 0;



        try

        {

            // Tự động lưu dữ liệu trước khi xuất

            SaveCvxdData(SelectedDocument);



            var progress = new Progress<CvxdExportProgress>(state =>

            {

                StatusMessage = state.Message;

                ProgressValue = state.Percent;

            });

            // Truyền thông tin và thực thi (exportAll = false để chỉ xuất các dòng được chọn mặc định)

            var outputPath = await service.ExportToWordAsync(SelectedDocument, CvxdInfo, GlobalConfig, [.. CvxdItems], exportAll: false, progress);

            

            // Trigger UI refresh in MainViewModel for real-time update

            MainVM.RefreshUI();



            StatusMessage = "Xuất file thành công!";

            ProgressValue = 100;

            var successMessage = string.IsNullOrWhiteSpace(outputPath)

                ? "Đã xuất hồ sơ CVXD thành công!"

                : $"Đã xuất hồ sơ CVXD thành công!\n\nFile lưu tại:\n{outputPath}";

            CvxdMessageBox.Show("Thông báo", successMessage, MessageBoxButton.OK, MessageBoxImage.Information);

        }

        catch (Exception ex)

        {

            CvxdMessageBox.Show("Lỗi", $"Lỗi khi xuất Word: {ex.Message}",

                MessageBoxButton.OK, MessageBoxImage.Error);

        }

        finally

        {

            await Task.Delay(1000); // Đợi 1 chút để user thấy chữ "Thành công"

            IsBusy = false;

            StatusMessage = string.Empty;

        }

    }

    [RelayCommand]
    private async Task ExportExcel()
    {
        if (SelectedDocument == null || CvxdItems.Count == 0 || IsBusy) return;

        try
        {
            SaveCvxdData(SelectedDocument);

            string outputDir = Path.Combine(
                SelectedDocument.FolderPath ?? AppDomain.CurrentDomain.BaseDirectory,
                "3.HSCL",
                "CVXD");

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string contractNumber = SanitizeFileName(SelectedDocument.ContractNumber ?? "CVXD");
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Lưu file Excel CVXD",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                InitialDirectory = outputDir,
                FileName = $"CVXD_{contractNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                AddExtension = true,
                DefaultExt = ".xlsx"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "Đang xuất Excel CVXD...";
            ProgressValue = 0;

            var service = new CvxdExportService();
            var document = SelectedDocument;
            var info = CvxdInfo;
            var items = CvxdItems.ToList();
            var fileName = dialog.FileName;

            var outputPath = await Task.Run(() => service.ExportToExcel(document, info, items, fileName));

            ProgressValue = 100;
            StatusMessage = "Xuất Excel thành công!";
            MainVM.RefreshUI();
            CvxdMessageBox.Show(
                "Thông báo",
                $"Đã xuất Excel CVXD thành công!\n\nFile lưu tại:\n{outputPath}",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            CvxdMessageBox.Show("Lỗi", $"Lỗi khi xuất Excel: {ex.Message}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            StatusMessage = string.Empty;
            ProgressValue = 0;
        }
    }

    [RelayCommand]
    private void OpenOtherTemplateFolder()
    {
        try
        {
            var path = SelectedOtherTemplatePath;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "open" });
        }
        catch (Exception ex)
        {
            CvxdMessageBox.Show("Lỗi", $"Không thể mở thư mục hồ sơ khác: {ex.Message}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void SetSelectedOtherTemplates(IEnumerable<OtherTemplateItem> templates)
    {
        SelectedOtherTemplates.Clear();
        foreach (var template in templates)
        {
            SelectedOtherTemplates.Add(template);
        }

        OnPropertyChanged(nameof(HasSelectedOtherTemplates));
        OnPropertyChanged(nameof(SelectedOtherTemplateCount));
        if (ShowSelectedOtherTemplatesOnly)
        {
            OtherTemplatesView?.Refresh();
        }
    }

    [RelayCommand]
    private async Task GenerateSelectedOtherTemplates()
    {
        if (SelectedDocument == null)
        {
            CvxdMessageBox.Show("Chưa chọn hợp đồng", "Vui lòng chọn hợp đồng trước khi tạo hồ sơ.", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var templates = SelectedOtherTemplates
            .Where(template => template.FilePath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (templates.Count == 0)
        {
            CvxdMessageBox.Show("Chưa chọn mẫu", "Vui lòng chọn ít nhất một mẫu Word để tạo hồ sơ.", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var outputPaths = new List<string>();
        var errors = new List<string>();
        foreach (var template in templates)
        {
            try
            {
                outputPaths.Add(await CreateOtherTemplateFileAsync(template));
            }
            catch (Exception ex)
            {
                errors.Add($"{template.Name}: {ex.Message}");
            }
        }

        if (outputPaths.Count > 0)
        {
            if (outputPaths.Count == 1)
            {
                Process.Start(new ProcessStartInfo { FileName = outputPaths[0], UseShellExecute = true });
            }
            else
            {
                var outputFolder = Path.GetDirectoryName(outputPaths[0]);
                if (!string.IsNullOrWhiteSpace(outputFolder))
                {
                    Process.Start(new ProcessStartInfo { FileName = outputFolder, UseShellExecute = true, Verb = "open" });
                }
            }

            ShowToast($"Đã tạo {outputPaths.Count} hồ sơ riêng biệt.");
        }

        if (errors.Count > 0)
        {
            CvxdMessageBox.Show("Một số hồ sơ chưa tạo được", string.Join(Environment.NewLine, errors), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task<string> CreateOtherTemplateFileAsync(OtherTemplateItem template)
    {
        var contractFolder = SelectedDocument!.FolderPath ?? Path.Combine(AppContext.BaseDirectory, "Generated");
        var outputFolder = Path.Combine(contractFolder, "HSCL_KHAC");
        var contractName = SanitizeFileName(SelectedDocument.ContractNumber ?? "HopDong");
        var templateName = SanitizeFileName(Path.GetFileNameWithoutExtension(template.FilePath));
        Directory.CreateDirectory(outputFolder);
        var outputPath = Path.Combine(outputFolder, $"{templateName}_{contractName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.docx");

        var service = new OtherQualityDocumentExportService();
        await service.GenerateAsync(SelectedDocument, CvxdInfo, GlobalConfig, template.FilePath, outputPath, OtherDocumentDate);
        ExportedFileRegistry.Record(SelectedDocument.FolderPath, outputPath, "Hồ sơ khác");
        return outputPath;
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '-');
        }

        return fileName.Trim();
    }



    private void ApplyImportedWorkbook(AcceptanceExcelImportResult imported, AcceptanceExcelImportMode importMode)

    {

        ApplyImportedSharedInfo(imported, importMode);



        if (imported.HasCvxdSheet)

        {

            var importedItems = imported.CvxdItems.ToList();

            foreach (var item in importedItems)

            {

                EnsureCustomFields(item);

            }



            if (importMode == AcceptanceExcelImportMode.Replace)
            {
                CvxdItems = new ObservableCollection<CvxdItem>(importedItems);
            }
            else
            {
                foreach (var item in importedItems)
                {
                    CvxdItems.Add(item);
                }
            }

            ReorderStt();

            SelectedCvxdItem = CvxdItems.FirstOrDefault();

        }



        if (imported.HasVtdvSheet)

        {

            if (importMode == AcceptanceExcelImportMode.Replace)
            {
                VtdvViewModel?.ReplaceImportedSessions(imported.VtdvSessions);
            }
            else
            {
                VtdvViewModel?.AppendImportedSessions(imported.VtdvSessions);
            }

        }



        AttachChangeTracking();

        RefreshPlaceholderConfiguration();

        IsDirty = true;

    }



    private void ApplyImportedSharedInfo(AcceptanceExcelImportResult imported, AcceptanceExcelImportMode importMode)

    {

        if (imported.CvxdCustomColumns.Count > 0)

        {

            ApplyImportedColumns(CvxdInfo.CustomColumns, imported.CvxdCustomColumns, importMode,
                columns => CvxdInfo.CustomColumns = columns);

        }



        if (imported.VtdvCustomColumns.Count > 0)

        {

            ApplyImportedColumns(CvxdInfo.VtdvCustomColumns, imported.VtdvCustomColumns, importMode,
                columns => CvxdInfo.VtdvCustomColumns = columns);

        }



        if (SelectedDocument != null)

        {

            SyncContractDisplayInfo(SelectedDocument);

        }



        OnPropertyChanged(nameof(CvxdInfo));

        VtdvViewModel?.NotifyProjectInfoChanged();

    }

    private static void ApplyImportedColumns(
        ObservableCollection<CvxdCustomColumn> existingColumns,
        IEnumerable<string> importedColumnNames,
        AcceptanceExcelImportMode importMode,
        Action<ObservableCollection<CvxdCustomColumn>> replaceColumns)
    {
        var names = importedColumnNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (importMode == AcceptanceExcelImportMode.Replace)
        {
            replaceColumns(new ObservableCollection<CvxdCustomColumn>(
                names.Select(name => new CvxdCustomColumn { Name = name })));
            return;
        }

        foreach (var name in names)
        {
            if (!existingColumns.Any(column => string.Equals(column.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            {
                existingColumns.Add(new CvxdCustomColumn { Name = name });
            }
        }
    }



    private bool ValidateItems()

    {

        bool isValid = true;

        foreach (var item in CvxdItems.Where(x => x.IsSelected))

        {

            item.HasError = false;



            // 1. Kiểm tra các trường luôn bắt buộc

            bool basicFieldsMissing = string.IsNullOrWhiteSpace(item.Tcnt) ||

                                   string.IsNullOrWhiteSpace(item.LoaiNt) ||

                                   string.IsNullOrWhiteSpace(item.NoiDung) ||

                                   string.IsNullOrWhiteSpace(item.NgayNt) ||

                                   string.IsNullOrWhiteSpace(item.BatDau) ||

                                   string.IsNullOrWhiteSpace(item.KetThuc) ||

                                   string.IsNullOrWhiteSpace(item.SoChecklist);



            // 2. Kiểm tra các trường liên quan đến Checklist (Chỉ bắt buộc nếu SoChecklist != "0")

            bool checklistFieldsMissing = false;

            string soChecklist = item.SoChecklist?.Trim() ?? "0";

            if (soChecklist != "0")

            {

                checklistFieldsMissing = string.IsNullOrWhiteSpace(item.LoaiBb) ||

                                       string.IsNullOrWhiteSpace(item.TenNt);

            }



            if (basicFieldsMissing || checklistFieldsMissing)

            {

                item.HasError = true;

                isValid = false;

            }

        }

        return isValid;

    }



    #region JSON Save/Load



    partial void OnAllSelectedChanged(bool value)

    {

        if (_isSyncingSelection) return;

        

        _isSyncingSelection = true;

        try

        {

            foreach (var item in CvxdItems)

            {

                item.IsSelected = value;

            }

        }

        finally

        {

            _isSyncingSelection = false;

        }

    }



    private static string GetCvxdDataPath(Document document)

    {

        if (string.IsNullOrEmpty(document.FolderPath))

        {

            // Fallback: lưu vào AppData

            var appDataPath = Path.Combine(

                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),

                "DocumentManagement", "CvxdData");

            Directory.CreateDirectory(appDataPath);

            return Path.Combine(appDataPath, $"cvxd_{document.Id}.json");

        }



        return Path.Combine(document.FolderPath, "cvxd_data.json");

    }



    private void SaveCvxdData(Document document)

    {

        try

        {

            var dataPath = GetCvxdDataPath(document);

            RemoveSystemPlaceholders(CvxdInfo.CustomPlaceholders);



            var data = new CvxdDataFile

            {

                Info = CvxdInfo,

                Items = [.. CvxdItems]

            };



            var json = JsonSerializer.Serialize(data, _jsonOptions);

            File.WriteAllText(dataPath, json);

        }

        catch (Exception ex)

        {

            System.Windows.MessageBox.Show($"Lỗi lưu dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);

        }

    }



    private void LoadCvxdData(Document document)

    {

        var dataPath = GetCvxdDataPath(document);



        CvxdItems.Clear();

        CvxdInfo = new CvxdInfo();



        // Nếu là hợp đồng mới (chưa có file dữ liệu), nạp Placeholder từ mẫu dùng chung

        if (!File.Exists(dataPath))

        {

            if (GlobalConfig.GlobalPlaceholders != null && GlobalConfig.GlobalPlaceholders.Count > 0)

            {

                CvxdInfo.CustomPlaceholders = new ObservableCollection<CvxdCustomPlaceholder>(GlobalConfig.GlobalPlaceholders);

                RemoveSystemPlaceholders(CvxdInfo.CustomPlaceholders);

            }

            else

            {

                // Nạp từ file mặc định nếu lần đầu sử dụng và chưa có GlobalConfig

                LoadDefaultPlaceholders(CvxdInfo);

            }

            // Auto-populate from document

            CvxdInfo.TenChuDauTu = document.EmployerName ?? "";

            CvxdInfo.KyHieuChuDauTu = document.EmployerCode ?? "";

            CvxdInfo.TenNhaThau = document.ContractorName ?? "";

            CvxdInfo.KyHieuNhaThau = document.ContractorCode ?? "";

            CvxdInfo.TenDuAn = document.ProjectName ?? "";

            CvxdInfo.TenCongTrinh = document.ConstructionName ?? "";

            CvxdInfo.TenHangMuc = document.ItemName ?? "";

            CvxdInfo.DiaDiem = document.Location ?? "";

            SyncContractDisplayInfo(document);

            return;

        }



        try

        {

            var json = File.ReadAllText(dataPath);

            var data = JsonSerializer.Deserialize<CvxdDataFile>(json, _jsonOptions);



            if (data != null)

            {

                if (data.Info != null)

                {

                    CvxdInfo = data.Info;

                    // Nếu hợp đồng đã lưu nhưng danh sách placeholder trống, cũng nạp từ mẫu dùng chung

                    if ((CvxdInfo.CustomPlaceholders == null || CvxdInfo.CustomPlaceholders.Count == 0))

                    {

                        if (GlobalConfig.GlobalPlaceholders != null && GlobalConfig.GlobalPlaceholders.Count > 0)

                        {

                            CvxdInfo.CustomPlaceholders = new ObservableCollection<CvxdCustomPlaceholder>(GlobalConfig.GlobalPlaceholders);

                            RemoveSystemPlaceholders(CvxdInfo.CustomPlaceholders);

                        }

                        else

                        {

                            LoadDefaultPlaceholders(CvxdInfo);

                        }

                    }

                    else

                    {

                        RemoveSystemPlaceholders(CvxdInfo.CustomPlaceholders);

                    }



                    // Tự động cập nhật nếu dữ liệu trong CVXD đang bị trống hoặc bị sai lệch (legacy bug)

                    if (!string.IsNullOrWhiteSpace(document.EmployerName))

                    {

                        // Nếu trống HOẶC đang khớp với InvestorName (nhưng 2 cái này khác nhau) -> Cập nhật lại cho đúng

                        if (string.IsNullOrWhiteSpace(CvxdInfo.TenChuDauTu) || 

                            (CvxdInfo.TenChuDauTu == document.InvestorName && document.InvestorName != document.EmployerName))

                        {

                            CvxdInfo.TenChuDauTu = document.EmployerName;

                        }

                    }



                    if (string.IsNullOrWhiteSpace(CvxdInfo.TenNhaThau) && !string.IsNullOrWhiteSpace(document.ContractorName))

                        CvxdInfo.TenNhaThau = document.ContractorName;

                    if (string.IsNullOrWhiteSpace(CvxdInfo.KyHieuChuDauTu) && !string.IsNullOrWhiteSpace(document.EmployerCode))

                        CvxdInfo.KyHieuChuDauTu = document.EmployerCode;

                    if (string.IsNullOrWhiteSpace(CvxdInfo.KyHieuNhaThau) && !string.IsNullOrWhiteSpace(document.ContractorCode))

                        CvxdInfo.KyHieuNhaThau = document.ContractorCode;

                    

                    if (string.IsNullOrWhiteSpace(CvxdInfo.TenDuAn) && !string.IsNullOrWhiteSpace(document.ProjectName))

                        CvxdInfo.TenDuAn = document.ProjectName;



                    if (string.IsNullOrWhiteSpace(CvxdInfo.TenCongTrinh) && !string.IsNullOrWhiteSpace(document.ConstructionName))

                        CvxdInfo.TenCongTrinh = document.ConstructionName;



                    if (string.IsNullOrWhiteSpace(CvxdInfo.TenHangMuc) && !string.IsNullOrWhiteSpace(document.ItemName))

                        CvxdInfo.TenHangMuc = document.ItemName;



                    if (string.IsNullOrWhiteSpace(CvxdInfo.DiaDiem) && !string.IsNullOrWhiteSpace(document.Location))

                        CvxdInfo.DiaDiem = document.Location;

                    SyncContractDisplayInfo(document);

                }



                if (data.Items != null)

                {

                    foreach (var item in data.Items)

                    {

                        EnsureCustomFields(item);

                        CvxdItems.Add(item);

                    }

                }

            }

        }

        catch (Exception ex)

        {

            System.Windows.MessageBox.Show($"Không thể tải dữ liệu CVXD: {ex.Message}", "Cảnh báo",

                MessageBoxButton.OK, MessageBoxImage.Warning);

        }

    }



    private void SyncContractDisplayInfo(Document document)

    {

        CvxdInfo.DisplayContractNumber = document.ContractNumber ?? string.Empty;

        CvxdInfo.DisplaySignDate = document.SignDate.ToString("dd/MM/yyyy");

        CvxdInfo.NgayKyHD = CvxdInfo.DisplaySignDate;

    }



    private static void LoadDefaultPlaceholders(CvxdInfo info)

    {

        try

        {

            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "cvxd_placeholder_defaults.json");

            if (File.Exists(configPath))

            {

                var json = File.ReadAllText(configPath);

                using var doc = JsonDocument.Parse(json);

                var defaults = JsonSerializer.Deserialize<List<CvxdCustomPlaceholder>>(json, _jsonOptions);

                if (defaults != null)

                {

                    info.CustomPlaceholders = new ObservableCollection<CvxdCustomPlaceholder>(

                        defaults.Where(p => !PlaceholderRegistry.IsSystemPlaceholder(p.Key)));

                }

            }

        }

        catch { /* Ignore */ }

    }



    private static void RemoveSystemPlaceholders(ObservableCollection<CvxdCustomPlaceholder>? placeholders)

    {

        if (placeholders == null) return;



        for (int i = placeholders.Count - 1; i >= 0; i--)

        {

            if (PlaceholderRegistry.IsSystemPlaceholder(placeholders[i].Key))

            {

                placeholders.RemoveAt(i);

            }

        }

    }



    #endregion



    public void Dispose()

    {

        _checklistWatcher?.Dispose();

        _wordTemplateWatcher?.Dispose();

        GC.SuppressFinalize(this);

    }



    [GeneratedRegex(@"^(\d{1,2})[h:]?(\d{0,2})$", RegexOptions.IgnoreCase)]

    private static partial Regex TimeRegex();

}



/// <summary>

/// Cấu trúc file JSON lưu trữ dữ liệu CVXD.

/// </summary>

public class CvxdDataFile

{

    public CvxdInfo? Info { get; set; }

    public List<CvxdItem>? Items { get; set; }

}



public class ChecklistTemplateItem
{
    public string Id { get; set; } = string.Empty;     // Ví dụ: 01
    public string Name { get; set; } = string.Empty;   // Ví dụ: Ống
    public string FullName { get; set; } = string.Empty; // Ví dụ: 01_Ống
    public string FilePath { get; set; } = string.Empty;
}

public class WordTemplateItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class OtherTemplateItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public partial class CvxdWindowViewModel
{
    private void LoadOtherTemplates()
    {
        try
        {
            AvailableOtherTemplates.Clear();
            SelectedOtherTemplates.Clear();
            OnPropertyChanged(nameof(HasSelectedOtherTemplates));
            OnPropertyChanged(nameof(SelectedOtherTemplateCount));
            var path = TemplateProfileResolver.GetOtherRoot(GlobalConfig, CvxdInfo?.TemplateProfile);
            if (!Directory.Exists(path))
            {
                OtherTemplatesView = new ListCollectionView(AvailableOtherTemplates);
                OtherTemplatesView.Filter = FilterOtherTemplate;
                SetOtherTemplateSort(descending: false);
                OnPropertyChanged(nameof(OtherTemplatesView));
                OnPropertyChanged(nameof(OtherTemplateCount));
                return;
            }

            var files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => !Path.GetFileName(file).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                .Where(file => new[] { ".docx", ".xlsx", ".pdf" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.CurrentCultureIgnoreCase);

            var descriptions = LoadOtherTemplateDescriptions();
            var index = 1;
            foreach (var file in files)
            {
                var fullName = Path.GetFileName(file);
                AvailableOtherTemplates.Add(new OtherTemplateItem
                {
                    Id = index.ToString(CultureInfo.InvariantCulture),
                    Name = Path.GetFileNameWithoutExtension(file),
                    FullName = fullName,
                    FilePath = file,
                    Description = descriptions.TryGetValue(fullName, out var description) ? description : string.Empty
                });
                index++;
            }

            OtherTemplatesView = new ListCollectionView(AvailableOtherTemplates);
            OtherTemplatesView.Filter = FilterOtherTemplate;
            SetOtherTemplateSort(descending: false);
            OnPropertyChanged(nameof(OtherTemplatesView));
            OnPropertyChanged(nameof(OtherTemplateCount));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi load hồ sơ chất lượng khác: {ex.Message}");
        }
    }

    private void LoadWordTemplates()
    {
        try
        {
            AvailableWordTemplates.Clear();

            string parentRoot = (!string.IsNullOrEmpty(GlobalConfig.TemplateFolderPath) && Directory.Exists(GlobalConfig.TemplateFolderPath))
                ? TemplateProfileResolver.GetTemplateRoot(GlobalConfig, CvxdInfo?.TemplateProfile)
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");

            int index = 1;
            if (!Directory.Exists(parentRoot))
            {
                return;
            }

            var files = Directory
                .GetFiles(parentRoot, "*.docx", SearchOption.AllDirectories)
                .Where(file => !Path.GetFileName(file).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.CurrentCultureIgnoreCase);

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var rawName = Path.GetFileNameWithoutExtension(file);
                var underscoreIndex = rawName.IndexOf('_');
                var displayName = underscoreIndex > 0 ? rawName[..underscoreIndex] : rawName;

                AvailableWordTemplates.Add(new WordTemplateItem
                {
                    Id = index.ToString(CultureInfo.InvariantCulture),
                    Name = displayName,
                    FullName = fileName,
                    FilePath = file
                });
                index += 1;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lỗi load word templates: {ex.Message}");
        }
    }
}
}
