using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentManagement.Models;
using DocumentManagement.Services;

namespace DocumentManagement.ViewModels;

/// <summary>
/// Quan ly danh sach dai dien va nguoi ky HSCL cho tung hop dong.
/// </summary>
public partial class SharedProjectInfoTestViewModel : ObservableObject
{
    private const string TvgsPartyKey = "TVGS";
    private const string EmployerPartyKey = "EMPLOYER";
    private const string ContractorPartyKey = "CONTRACTOR";
    private readonly RepresentativeLibraryService _representativeLibraryService = new();
    private CvxdInfo? _observedInfo;

    [ObservableProperty]
    private CvxdInfo _info = new();

    [ObservableProperty]
    private bool _isAdmin;

    public ObservableCollection<TestRepresentativeGroup> Groups { get; } = [];

    /// <summary>Thong bao cho man hinh cha de danh dau du lieu can luu.</summary>
    public Action? DataChanged { get; set; }

    /// <summary>Yeu cau man hinh cha hien thi toast dong bo voi phan he HSCL.</summary>
    public Action<string>? NotificationRequested { get; set; }

    public void LoadFromLegacy(CvxdInfo info)
    {
        if (_observedInfo != null)
            _observedInfo.PropertyChanged -= OnInfoPropertyChanged;

        Info = info;
        _observedInfo = info;
        _observedInfo.PropertyChanged += OnInfoPropertyChanged;
        Groups.Clear();
        var libraries = _representativeLibraryService.Load();

        EnsureLegacyRepresentative(info.TvgsRepresentatives, info.DaiDienTVGS, info.ChucVuTVGS);
        EnsureLegacyRepresentative(info.EmployerRepresentatives, info.DaiDienChuDauTu, info.ChucVuCDT);
        EnsureLegacyRepresentative(info.ContractorRepresentatives, info.DaiDienNhaThau, info.ChucVuNT);

        var tvgsGroup = CreateGroup(
            "TƯ VẤN GIÁM SÁT", "#2563EB", "DaiDienTVGS / ChucVuTVGS", info.TvgsRepresentatives,
            info.TenTuVanGiamSat, info.KyHieuTVGS, info.DaiDienTVGS, info.ChucVuTVGS,
            (companyName, companyCode) => { info.TenTuVanGiamSat = companyName; info.KyHieuTVGS = companyCode; },
            signer => { info.DaiDienTVGS = signer?.FullName ?? string.Empty; info.ChucVuTVGS = signer?.Position ?? string.Empty; });
        tvgsGroup.ConfigureLibraries(TvgsPartyKey, libraries);
        Groups.Add(tvgsGroup);

        var employerGroup = CreateGroup(
            "BÊN GIAO THẦU", "#D97706", "DaiDienNTC / ChucVuNTC", info.EmployerRepresentatives,
            info.TenChuDauTu, info.KyHieuChuDauTu, info.DaiDienChuDauTu, info.ChucVuCDT,
            (companyName, companyCode) => { info.TenChuDauTu = companyName; info.KyHieuChuDauTu = companyCode; },
            signer => { info.DaiDienChuDauTu = signer?.FullName ?? string.Empty; info.ChucVuCDT = signer?.Position ?? string.Empty; });
        employerGroup.ConfigureLibraries(EmployerPartyKey, libraries);
        Groups.Add(employerGroup);

        var contractorGroup = CreateGroup(
            "BÊN NHẬN THẦU", "#059669", "DaiDienNTP / ChucVuNTP", info.ContractorRepresentatives,
            info.TenNhaThau, info.KyHieuNhaThau, info.DaiDienNhaThau, info.ChucVuNT,
            (companyName, companyCode) => { info.TenNhaThau = companyName; info.KyHieuNhaThau = companyCode; },
            signer => { info.DaiDienNhaThau = signer?.FullName ?? string.Empty; info.ChucVuNT = signer?.Position ?? string.Empty; });
        contractorGroup.ConfigureLibraries(ContractorPartyKey, libraries);
        Groups.Add(contractorGroup);

        RefreshTemplateLocks();
    }

    private void OnInfoPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CvxdInfo.TemplateProfile))
            RefreshTemplateLocks();
    }

    private void RefreshTemplateLocks()
    {
        foreach (var group in Groups)
        {
            var lockMessage = group.PartyKey switch
            {
                TvgsPartyKey when !Info.IsTvgsSectionEnabled => "Không áp dụng với mẫu PKH",
                EmployerPartyKey when !Info.IsEmployerSectionEnabled => "Không áp dụng với mẫu Bà Nà",
                _ => string.Empty
            };

            group.LockMessage = lockMessage;
            group.IsInputEnabled = string.IsNullOrEmpty(lockMessage);
        }
    }

    [RelayCommand]
    private void LoadFromLegacy()
    {
        LoadFromLegacy(Info);
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private void AddRepresentative(TestRepresentativeGroup? group)
    {
        if (group == null) return;
        group.Members.Add(new ContractRepresentative());
        group.GoToLastPage();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private void RemoveRepresentative(ContractRepresentative? representative)
    {
        var group = Groups.FirstOrDefault(item => item.Members.Contains(representative!));
        if (group == null || representative == null) return;

        if (ReferenceEquals(group.SelectedSigner, representative)) group.SelectedSigner = null;
        group.Members.Remove(representative);
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private void LoadRepresentativeLibrary(TestRepresentativeGroup? group)
    {
        var profile = group?.SelectedLibrary;
        if (group == null || profile == null) return;

        group.CompanyName = profile.CompanyName;
        group.CompanyCode = profile.CompanyCode;
        group.Members.Clear();
        foreach (var member in profile.Members)
            group.Members.Add(new ContractRepresentative { FullName = member.FullName, Position = member.Position });

        group.ApplySigner(group.Members.FirstOrDefault(member =>
            string.Equals(member.FullName, profile.DefaultSignerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(member.Position, profile.DefaultSignerPosition, StringComparison.OrdinalIgnoreCase))
            ?? group.Members.FirstOrDefault(member =>
                string.Equals(member.FullName, profile.DefaultSignerName, StringComparison.OrdinalIgnoreCase)));

        group.LibraryName = profile.Name;
        group.LibraryStatus = $"Đã nạp mẫu: {profile.Name}";
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private void SaveRepresentativeLibrary(TestRepresentativeGroup? group)
    {
        if (group == null) return;

        var name = group.LibraryName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = group.SelectedLibrary?.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = string.IsNullOrWhiteSpace(group.CompanyName) ? group.Title : group.CompanyName.Trim();
            group.LibraryName = name;
        }

        var profiles = _representativeLibraryService.Load();
        var profile = profiles.FirstOrDefault(item =>
            string.Equals(item.PartyKey, group.PartyKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

        if (profile == null)
        {
            profile = new RepresentativeLibraryProfile { PartyKey = group.PartyKey, Name = name };
            profiles.Add(profile);
        }

        profile.CompanyName = group.CompanyName;
        profile.CompanyCode = group.CompanyCode;
        profile.DefaultSignerName = group.SelectedSigner?.FullName ?? string.Empty;
        profile.DefaultSignerPosition = group.SelectedSigner?.Position ?? string.Empty;
        profile.Members = group.Members
            .Select(member => new RepresentativeLibraryMember { FullName = member.FullName, Position = member.Position })
            .ToList();

        _representativeLibraryService.Save(profiles);
        foreach (var item in Groups) item.ConfigureLibraries(item.PartyKey, profiles);
        group.SelectedLibrary = group.LibraryProfiles.FirstOrDefault(item => item.Id == profile.Id);
        group.LibraryStatus = $"Đã lưu mẫu: {profile.Name}";
        NotificationRequested?.Invoke($"Đã lưu mẫu danh sách '{profile.Name}'.");
    }

    private TestRepresentativeGroup CreateGroup(
        string title,
        string accentColor,
        string placeholderHint,
        ObservableCollection<ContractRepresentative> members,
        string companyName,
        string companyCode,
        string? signerName,
        string? signerPosition,
        Action<string, string> onCompanyInfoChanged,
        Action<ContractRepresentative?> onSignerChanged)
    {
        var group = new TestRepresentativeGroup
        {
            Title = title,
            AccentColor = accentColor,
            PlaceholderHint = placeholderHint,
            Members = members,
            CompanyName = companyName,
            CompanyCode = companyCode,
            CompanyInfoChanged = (name, code) =>
            {
                onCompanyInfoChanged(name, code);
                DataChanged?.Invoke();
            },
            SignerChanged = signer =>
            {
                onSignerChanged(signer);
                DataChanged?.Invoke();
            },
            DataChanged = () => DataChanged?.Invoke()
        };

        group.SetInitialSigner(members.FirstOrDefault(item =>
            string.Equals(item.FullName, signerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Position, signerPosition, StringComparison.OrdinalIgnoreCase))
            ?? members.FirstOrDefault(item => string.Equals(item.FullName, signerName, StringComparison.OrdinalIgnoreCase)));
        return group;
    }

    private static void EnsureLegacyRepresentative(
        ObservableCollection<ContractRepresentative> representatives,
        string? fullName,
        string? position)
    {
        if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(position)) return;
        if (representatives.Any(item => string.Equals(item.FullName, fullName, StringComparison.OrdinalIgnoreCase))) return;
        representatives.Add(new ContractRepresentative { FullName = fullName ?? string.Empty, Position = position ?? string.Empty });
    }
}

public partial class TestRepresentativeGroup : ObservableObject
{
    private const int PageSize = 3;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _accentColor = "#2563EB";
    [ObservableProperty] private string _placeholderHint = string.Empty;
    [ObservableProperty] private ObservableCollection<ContractRepresentative> _members = [];
    [ObservableProperty] private ContractRepresentative? _selectedSigner;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string _companyCode = string.Empty;
    [ObservableProperty] private RepresentativeLibraryProfile? _selectedLibrary;
    [ObservableProperty] private string _libraryName = string.Empty;
    [ObservableProperty] private string _libraryStatus = string.Empty;
    [ObservableProperty] private bool _isInputEnabled = true;
    [ObservableProperty] private string _lockMessage = string.Empty;
    [ObservableProperty] private int _currentPage = 1;
    private bool _isSettingInitialSigner;

    public string PartyKey { get; private set; } = string.Empty;
    public bool IsLocked => !IsInputEnabled;
    public ObservableCollection<RepresentativeLibraryProfile> LibraryProfiles { get; } = [];
    public ObservableCollection<ContractRepresentative> CurrentPageMembers { get; } = [];
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Members.Count / (double)PageSize));
    public string PageSummary => Members.Count == 0
        ? "Chưa có đại diện"
        : $"Trang {CurrentPage}/{TotalPages} · {Members.Count} người";
    public bool CanGoToPreviousPage => CurrentPage > 1;
    public bool CanGoToNextPage => CurrentPage < TotalPages;

    internal Action<string, string>? CompanyInfoChanged { get; init; }
    internal Action<ContractRepresentative?>? SignerChanged { get; init; }
    internal Action? DataChanged { get; init; }

    public TestRepresentativeGroup()
    {
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Members))
            {
                AttachMembers();
                RefreshCurrentPage();
            }
        };
    }

    partial void OnIsInputEnabledChanged(bool value) => OnPropertyChanged(nameof(IsLocked));

    partial void OnSelectedSignerChanged(ContractRepresentative? value)
    {
        if (!_isSettingInitialSigner) SignerChanged?.Invoke(value);
    }

    partial void OnCompanyNameChanged(string value) => CompanyInfoChanged?.Invoke(value, CompanyCode);

    partial void OnCompanyCodeChanged(string value) => CompanyInfoChanged?.Invoke(CompanyName, value);

    public void SetInitialSigner(ContractRepresentative? signer)
    {
        _isSettingInitialSigner = true;
        SelectedSigner = signer;
        _isSettingInitialSigner = false;
    }

    public void ApplySigner(ContractRepresentative? signer)
    {
        SetInitialSigner(signer);
        SignerChanged?.Invoke(signer);
    }

    public void ConfigureLibraries(string partyKey, IEnumerable<RepresentativeLibraryProfile> profiles)
    {
        PartyKey = partyKey;
        var selectedId = SelectedLibrary?.Id;
        LibraryProfiles.Clear();
        foreach (var profile in profiles.Where(profile =>
                     string.Equals(profile.PartyKey, partyKey, StringComparison.OrdinalIgnoreCase))
                 .OrderBy(profile => profile.Name))
            LibraryProfiles.Add(profile);

        SelectedLibrary = LibraryProfiles.FirstOrDefault(profile => profile.Id == selectedId);
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void FirstPage()
    {
        CurrentPage = 1;
        RefreshCurrentPage();
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void PreviousPage()
    {
        CurrentPage--;
        RefreshCurrentPage();
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void NextPage()
    {
        CurrentPage++;
        RefreshCurrentPage();
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void LastPage()
    {
        CurrentPage = TotalPages;
        RefreshCurrentPage();
    }

    public void GoToLastPage()
    {
        CurrentPage = TotalPages;
        RefreshCurrentPage();
    }

    private void RefreshCurrentPage()
    {
        CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);
        CurrentPageMembers.Clear();
        foreach (var member in Members.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
            CurrentPageMembers.Add(member);

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
        FirstPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        LastPageCommand.NotifyCanExecuteChanged();
    }

    private void AttachMembers()
    {
        Members.CollectionChanged += (_, args) =>
        {
            if (args.NewItems != null)
            {
                foreach (ContractRepresentative member in args.NewItems)
                    member.PropertyChanged += (_, _) => DataChanged?.Invoke();
            }

            RefreshCurrentPage();
            DataChanged?.Invoke();
        };

        foreach (var member in Members)
            member.PropertyChanged += (_, _) => DataChanged?.Invoke();

        RefreshCurrentPage();
    }
}
