using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using DocumentManagement;
using DocumentManagement.Models;
using DocumentManagement.Services;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using DocumentManagement.Utilities;

namespace DocumentManagement.ViewModels;

public partial class VtdvViewModel : ObservableObject
{
    private readonly MainViewModel _mainVM;
    private readonly VtdvExportService _exportService;
    private readonly CvxdWindowViewModel _cvxdVM;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();
    private bool _suppressDirtyTracking;

    [ObservableProperty]
    private ObservableCollection<Document> _documents = [];

    [ObservableProperty]
    private Document? _selectedDocument;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    [ObservableProperty]
    private ObservableCollection<VtdvSession> _sessions = [];

    [ObservableProperty]
    private VtdvSession? _selectedSession;

    [ObservableProperty]
    private VtdvItem? _selectedVtdvItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _templateFilePath = string.Empty;

    public VtdvViewModel(MainViewModel mainVM, VtdvExportService exportService, CvxdWindowViewModel cvxdVM)
    {
        _mainVM = mainVM;
        _exportService = exportService;
        _cvxdVM = cvxdVM;
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            PropertyNameCaseInsensitive = true
        };

        Documents = new ObservableCollection<Document>(_mainVM.Documents);
        
        // Theo dõi thay đổi hợp đồng từ MainViewModel
        _mainVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedDocument))
            {
                RefreshContext();
            }
        };

        // Theo dõi thay đổi hợp đồng từ CvxdWindowViewModel (nếu chuyển trong tab CVXD)
        _cvxdVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CvxdWindowViewModel.SelectedDocument))
            {
                RefreshContext();
            }
        };

        Sessions.CollectionChanged += Sessions_CollectionChanged;
        LoadContractContext();
    }

    // Proxy properties to share data with CVXD
    public CvxdInfo ProjectInfo => _cvxdVM.CvxdInfo;
    public CvxdGlobalConfig GlobalConfig => _cvxdVM.GlobalConfig;
    public bool IsAdmin => _cvxdVM.IsAdmin;

    private void RefreshContext()
    {
        if (_mainVM.SelectedDocument != null && SelectedDocument != _mainVM.SelectedDocument)
        {
            RunWithoutDirtyTracking(() =>
            {
                SelectedDocument = _mainVM.SelectedDocument;
            });
        }
    }

    private void LoadContractContext()
    {
        RunWithoutDirtyTracking(() =>
        {
            SelectedDocument = _mainVM.SelectedDocument;
            if (SelectedDocument != null)
            {
                LoadVtdvData(SelectedDocument);
            }
        });
    }

    partial void OnSelectedDocumentChanged(Document? value)
    {
        if (value != null)
        {
            // Cập nhật ProjectInfo và tải dữ liệu
            OnPropertyChanged(nameof(ProjectInfo));
            LoadVtdvData(value);
            
            // Đồng bộ với MainViewModel nếu cần
            if (_mainVM.SelectedDocument != value)
            {
                _mainVM.SelectedDocument = value;
            }

            // Sync với CVXD nếu cần (nhưng không gây loop)
            if (_cvxdVM.SelectedDocument != value)
            {
                _cvxdVM.SelectedDocument = value;
            }
        }
    }

    private static string GetVtdvDataPath(Document document)
    {
        if (string.IsNullOrEmpty(document.FolderPath)) return string.Empty;
        return Path.Combine(document.FolderPath, "vtdv_data.json");
    }

    private void LoadVtdvData(Document document)
    {
        RunWithoutDirtyTracking(() =>
        {
            var dataPath = GetVtdvDataPath(document);
            var contractNum = document.ContractNumber ?? "Không xác định";
            
            _mainVM.ShowToast($"Đang tải VTĐV cho HĐ: {contractNum}", true);
            
            // Luôn clear trước khi load mới
            Sessions.Clear();
            
            if (!string.IsNullOrEmpty(dataPath) && File.Exists(dataPath))
            {
                try
                {
                    string json = File.ReadAllText(dataPath);
                    var loadedSessions = JsonSerializer.Deserialize<List<VtdvSession>>(json, _jsonOptions);
                    if (loadedSessions != null)
                    {
                        foreach (var s in loadedSessions)
                        {
                            EnsureSessionCustomFields(s);
                            Sessions.Add(s);
                        }
                        SelectedSession = Sessions.FirstOrDefault();
                    }
                }
                catch (Exception ex)
                {
                    _mainVM.ShowToast($"Lỗi khi tải dữ liệu VTĐV: {ex.Message}", false);
                }
            }

            // If no sessions loaded, create a default one
            if (Sessions.Count == 0)
            {
                AddNewSession();
            }
            else
            {
                SyncSessionWithContract();
            }
        });
    }

    private void SyncSessionWithContract()
    {
        RunWithoutDirtyTracking(() =>
        {
            if (SelectedDocument != null && SelectedSession != null)
            {
                SelectedSession.DuAn = ProjectInfo.TenDuAn;
                SelectedSession.CongTrinh = ProjectInfo.TenCongTrinh;
                SelectedSession.HangMuc = ProjectInfo.TenHangMuc;
                SelectedSession.NhaThau = ProjectInfo.TenNhaThau;
                SelectedSession.DiaDiem = ProjectInfo.DiaDiem;
            }
            UpdateSessionNumbers();
            foreach (var session in Sessions)
            {
                EnsureSessionCustomFields(session);
            }
            AttachVtdvChangeTracking();
        });
    }

    private void Sessions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (VtdvSession session in e.OldItems)
            {
                UntrackSession(session);
            }
        }

        if (e.NewItems != null)
        {
            foreach (VtdvSession session in e.NewItems)
            {
                TrackSession(session);
            }
        }

        UpdateSessionNumbers();
        if (!_suppressDirtyTracking)
        {
            _cvxdVM.IsDirty = true;
        }

        NotifyMoveVtdvItemCommands();
    }

    private void AttachVtdvChangeTracking()
    {
        foreach (var session in Sessions)
        {
            TrackSession(session);
        }
    }

    private void TrackSession(VtdvSession session)
    {
        session.PropertyChanged -= OnVtdvDataChanged;
        session.PropertyChanged += OnVtdvDataChanged;
        session.Items.CollectionChanged -= VtdvItems_CollectionChanged;
        session.Items.CollectionChanged += VtdvItems_CollectionChanged;

        foreach (var item in session.Items)
        {
            TrackItem(item);
        }
    }

    private void UntrackSession(VtdvSession session)
    {
        session.PropertyChanged -= OnVtdvDataChanged;
        session.Items.CollectionChanged -= VtdvItems_CollectionChanged;
        foreach (var item in session.Items)
        {
            item.PropertyChanged -= OnVtdvDataChanged;
        }
    }

    private void VtdvItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (VtdvItem item in e.OldItems)
            {
                item.PropertyChanged -= OnVtdvDataChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (VtdvItem item in e.NewItems)
            {
                TrackItem(item);
            }
        }

        if (!_suppressDirtyTracking)
        {
            _cvxdVM.IsDirty = true;
        }
    }

    private void TrackItem(VtdvItem item)
    {
        item.PropertyChanged -= OnVtdvDataChanged;
        item.PropertyChanged += OnVtdvDataChanged;
    }

    private void OnVtdvDataChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressDirtyTracking) return;
        _cvxdVM.IsDirty = true;
    }

    private void UpdateSessionNumbers()
    {
        if (SelectedDocument == null) return;
        var contractShort = SelectedDocument.ContractNumber?.Split('/').FirstOrDefault() ?? "NEW";
        for (int i = 0; i < Sessions.Count; i++)
        {
            var expectedNumber = $"{(i + 1):D2}/VT-{contractShort}";

            if (Sessions[i].SoBienBan != expectedNumber)
            {
                Sessions[i].SoBienBan = expectedNumber;
            }
        }
    }

    [RelayCommand]
    private void AddNewSession()
    {
        var newSession = new VtdvSession 
        { 
            SoBienBan = $"BB-{Sessions.Count + 1:D2}",
            NgayNghiệmThu = DateTime.Now.ToString("dd/MM/yyyy")
        };

        if (SelectedDocument != null)
        {
            newSession.DuAn = ProjectInfo.TenDuAn;
            newSession.CongTrinh = ProjectInfo.TenCongTrinh;
            newSession.HangMuc = ProjectInfo.TenHangMuc;
            newSession.NhaThau = ProjectInfo.TenNhaThau;
            newSession.DiaDiem = ProjectInfo.DiaDiem;
        }

        Sessions.Add(newSession);
        EnsureSessionCustomFields(newSession);
        SelectedSession = newSession;
    }

    [RelayCommand]
    private void DeleteSession(VtdvSession? session)
    {
        if (session != null)
        {
            var index = Sessions.IndexOf(session);
            if (index >= 0)
            {
                PushUndoAction(new CollectionChangeAction<VtdvSession>(Sessions, session, index, false));
            }

            Sessions.Remove(session);
            if (SelectedSession == session)
                SelectedSession = Sessions.FirstOrDefault();
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedSessionUp))]
    private void MoveSelectedSessionUp(VtdvSession? session = null)
    {
        MoveSession(session ?? SelectedSession, -1);
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedSessionDown))]
    private void MoveSelectedSessionDown(VtdvSession? session = null)
    {
        MoveSession(session ?? SelectedSession, 1);
    }

    private bool CanMoveSelectedSessionUp(VtdvSession? session = null)
    {
        var candidate = session ?? SelectedSession;
        return candidate != null && Sessions.IndexOf(candidate) > 0;
    }

    private bool CanMoveSelectedSessionDown(VtdvSession? session = null)
    {
        var candidate = session ?? SelectedSession;
        var index = candidate == null ? -1 : Sessions.IndexOf(candidate);
        return index >= 0 && index < Sessions.Count - 1;
    }

    [RelayCommand]
    private void SortSessionsByDate()
    {
        if (Sessions.Count <= 1) return;

        var selected = SelectedSession;
        var oldOrder = Sessions.ToList();
        var ordered = Sessions
            .Select((session, index) => new { Session = session, Date = ParseSessionDate(session.NgayNghiệmThu), Index = index })
            .OrderBy(x => x.Date ?? DateTime.MaxValue)
            .ThenBy(x => x.Index)
            .Select(x => x.Session)
            .ToList();

        if (oldOrder.SequenceEqual(ordered)) return;

        for (var targetIndex = 0; targetIndex < ordered.Count; targetIndex++)
        {
            var currentIndex = Sessions.IndexOf(ordered[targetIndex]);
            if (currentIndex >= 0 && currentIndex != targetIndex)
            {
                Sessions.Move(currentIndex, targetIndex);
            }
        }

        UpdateSessionNumbers();
        SelectedSession = selected;
        PushUndoAction(new ReorderCollectionAction<VtdvSession>(Sessions, oldOrder, ordered));
        if (!_suppressDirtyTracking)
        {
            _cvxdVM.IsDirty = true;
        }
        _mainVM.ShowToast("Đã sắp xếp biên bản VTĐV theo ngày nghiệm thu.", true);
    }

    private void MoveSession(VtdvSession? session, int direction)
    {
        if (session == null) return;

        var currentIndex = Sessions.IndexOf(session);
        if (currentIndex < 0) return;

        var targetIndex = currentIndex + direction;
        if (targetIndex < 0 || targetIndex >= Sessions.Count) return;

        var oldOrder = Sessions.ToList();
        Sessions.Move(currentIndex, targetIndex);
        UpdateSessionNumbers();
        SelectedSession = session;
        PushUndoAction(new ReorderCollectionAction<VtdvSession>(Sessions, oldOrder, Sessions.ToList()));
        if (!_suppressDirtyTracking)
        {
            _cvxdVM.IsDirty = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedVtdvItemUp))]
    private void MoveSelectedVtdvItemUp()
    {
        MoveSelectedVtdvItem(-1);
    }

    private bool CanMoveSelectedVtdvItemUp()
    {
        if (!IsAdmin || SelectedSession == null || SelectedVtdvItem == null) return false;
        return SelectedSession.Items.IndexOf(SelectedVtdvItem) > 0;
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedVtdvItemDown))]
    private void MoveSelectedVtdvItemDown()
    {
        MoveSelectedVtdvItem(1);
    }

    private bool CanMoveSelectedVtdvItemDown()
    {
        if (!IsAdmin || SelectedSession == null || SelectedVtdvItem == null) return false;
        var index = SelectedSession.Items.IndexOf(SelectedVtdvItem);
        return index >= 0 && index < SelectedSession.Items.Count - 1;
    }

    private void MoveSelectedVtdvItem(int direction)
    {
        var session = SelectedSession;
        var item = SelectedVtdvItem;
        if (session == null || item == null) return;

        var currentIndex = session.Items.IndexOf(item);
        if (currentIndex < 0) return;

        var targetIndex = currentIndex + direction;
        if (targetIndex < 0 || targetIndex >= session.Items.Count) return;

        var oldOrder = session.Items.ToList();
        session.Items.Move(currentIndex, targetIndex);
        RenumberItems(session);
        SelectedVtdvItem = item;
        PushUndoAction(new ReorderCollectionAction<VtdvItem>(session.Items, oldOrder, session.Items.ToList()));
        if (!_suppressDirtyTracking)
        {
            _cvxdVM.IsDirty = true;
        }

        NotifyMoveVtdvItemCommands();
        MoveSelectedSessionUpCommand.NotifyCanExecuteChanged();
        MoveSelectedSessionDownCommand.NotifyCanExecuteChanged();
    }

    private void NotifyMoveVtdvItemCommands()
    {
        MoveSelectedVtdvItemUpCommand.NotifyCanExecuteChanged();
        MoveSelectedVtdvItemDownCommand.NotifyCanExecuteChanged();
    }

    private void PushUndoAction(IUndoableAction action)
    {
        if (!IsAdmin) return;

        _undoStack.Push(action);
        _redoStack.Clear();
        UndoVtdvCommand.NotifyCanExecuteChanged();
        RedoVtdvCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUndoVtdv))]
    private void UndoVtdv()
    {
        if (_undoStack.Count == 0) return;

        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        UpdateSessionNumbers();
        RenumberAllItems();
        SelectedSession ??= Sessions.FirstOrDefault();
        if (SelectedSession != null && (SelectedVtdvItem == null || !SelectedSession.Items.Contains(SelectedVtdvItem)))
        {
            SelectedVtdvItem = SelectedSession.Items.FirstOrDefault();
        }
        if (!_suppressDirtyTracking)
        {
            _cvxdVM.IsDirty = true;
        }

        UndoVtdvCommand.NotifyCanExecuteChanged();
        RedoVtdvCommand.NotifyCanExecuteChanged();
    }

    private bool CanUndoVtdv() => IsAdmin && _undoStack.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRedoVtdv))]
    private void RedoVtdv()
    {
        if (_redoStack.Count == 0) return;

        var action = _redoStack.Pop();
        action.Redo();
        _undoStack.Push(action);
        UpdateSessionNumbers();
        RenumberAllItems();
        SelectedSession ??= Sessions.FirstOrDefault();
        if (SelectedSession != null && (SelectedVtdvItem == null || !SelectedSession.Items.Contains(SelectedVtdvItem)))
        {
            SelectedVtdvItem = SelectedSession.Items.FirstOrDefault();
        }
        if (!_suppressDirtyTracking)
        {
            _cvxdVM.IsDirty = true;
        }

        UndoVtdvCommand.NotifyCanExecuteChanged();
        RedoVtdvCommand.NotifyCanExecuteChanged();
    }

    private bool CanRedoVtdv() => IsAdmin && _redoStack.Count > 0;

    private static DateTime? ParseSessionDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy" };
        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.Date;
        }

        return DateTime.TryParse(value, out date) ? date.Date : null;
    }

    public int CountFindReplaceMatches(string find)
    {
        if (string.IsNullOrEmpty(find)) return 0;
        return BuildFindReplaceFields(SelectedSession).Sum(field => CountOccurrences(field.Get()));

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

    public int ReplaceAll(string find, string replacement)
    {
        if (string.IsNullOrEmpty(find)) return 0;
        var changed = 0;
        foreach (var field in BuildFindReplaceFields(SelectedSession))
        {
            var value = field.Get();
            var updated = value.Replace(find, replacement ?? string.Empty, StringComparison.CurrentCultureIgnoreCase);
            if (updated == value) continue;
            field.Set(updated);
            changed += CountOccurrences(value);
        }

        if (changed > 0) _cvxdVM.IsDirty = true;
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

    public int ReplaceNext(string find, string replacement)
    {
        if (string.IsNullOrEmpty(find)) return 0;
        foreach (var field in BuildFindReplaceFields(SelectedSession))
        {
            var value = field.Get();
            var index = value.IndexOf(find, StringComparison.CurrentCultureIgnoreCase);
            if (index < 0) continue;
            field.Set(value.Remove(index, find.Length).Insert(index, replacement ?? string.Empty));
            _cvxdVM.IsDirty = true;
            return 1;
        }
        return 0;
    }

    public VtdvItem? FindFirstItem(string find)
    {
        if (string.IsNullOrEmpty(find) || SelectedSession == null) return null;
        return SelectedSession.Items.FirstOrDefault(item =>
            new[] { item.DoiTuong, item.SoPhieuXuat, item.SoLuong, item.DonVi,
                    item.ThuongHieu, item.MaSanPham, item.ChatLuong, item.GhiChu }
                .Concat(item.CustomFields.Select(field => field.Value))
                .Any(value => value.Contains(find, StringComparison.CurrentCultureIgnoreCase)));
    }

    public VtdvItem? FindNextItem(string find, bool advance)
    {
        var matches = SelectedSession?.Items.Where(item =>
            new[] { item.DoiTuong, item.SoPhieuXuat, item.SoLuong, item.DonVi,
                    item.ThuongHieu, item.MaSanPham, item.ChatLuong, item.GhiChu }
                .Concat(item.CustomFields.Select(field => field.Value))
                .Any(value => value.Contains(find, StringComparison.CurrentCultureIgnoreCase))).ToList() ?? [];
        if (matches.Count == 0) return null;
        if (!advance || SelectedVtdvItem == null) return matches[0];
        var index = matches.IndexOf(SelectedVtdvItem);
        return matches[(index + 1 + matches.Count) % matches.Count];
    }

    private IEnumerable<(Func<string> Get, Action<string> Set)> BuildFindReplaceFields(VtdvSession? session)
    {
        if (session == null) yield break;
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

    [RelayCommand]
    private void AddItem(VtdvItem? anchorItem = null)
    {
        if (SelectedSession == null) return;
        var item = new VtdvItem();
        EnsureItemCustomFields(item);
        var insertAfter = anchorItem ?? SelectedVtdvItem;
        var index = insertAfter != null ? SelectedSession.Items.IndexOf(insertAfter) : -1;
        var newIndex = index + 1;
        if (index >= 0)
        {
            SelectedSession.Items.Insert(newIndex, item);
        }
        else
        {
            SelectedSession.Items.Add(item);
            newIndex = SelectedSession.Items.Count - 1;
        }
        RenumberItems(SelectedSession);
        SelectedVtdvItem = item;
        PushUndoAction(new CollectionChangeAction<VtdvItem>(SelectedSession.Items, item, newIndex, true));
    }

    [RelayCommand]
    private void RemoveItem(VtdvItem? item)
    {
        if (item != null)
        {
            var session = SelectedSession;
            var index = session?.Items.IndexOf(item) ?? -1;
            if (index >= 0 && session != null)
            {
                PushUndoAction(new CollectionChangeAction<VtdvItem>(session.Items, item, index, false));
            }

            session?.Items.Remove(item);
            if (session != null)
            {
                RenumberItems(session);
            }
        }
    }

    public void RecordVtdvCellEditUndo(object target, string propertyName, object? oldValue, object? newValue)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return;
        if (Equals(oldValue, newValue)) return;

        PushUndoAction(new PropertyChangeAction(target, propertyName, oldValue, newValue, _ => _cvxdVM.IsDirty = true));
    }

    public void RecordVtdvCellEditBatchUndo(IEnumerable<IUndoableAction> actions)
    {
        var batch = new CompositeUndoAction(actions);
        if (batch.HasActions) PushUndoAction(batch);
    }

    partial void OnSelectedSessionChanged(VtdvSession? value)
    {
        if (value != null)
        {
            EnsureSessionCustomFields(value);
            SelectedVtdvItem = value.Items.FirstOrDefault();
        }
        else
        {
            SelectedVtdvItem = null;
        }

        NotifyMoveVtdvItemCommands();
        MoveSelectedSessionUpCommand.NotifyCanExecuteChanged();
        MoveSelectedSessionDownCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedVtdvItemChanged(VtdvItem? value)
    {
        NotifyMoveVtdvItemCommands();
    }

    private static void RenumberItems(VtdvSession session)
    {
        for (var i = 0; i < session.Items.Count; i++)
        {
            session.Items[i].Stt = (i + 1).ToString();
        }
    }

    private void RenumberAllItems()
    {
        foreach (var session in Sessions)
        {
            RenumberItems(session);
        }
    }

    private void EnsureSessionCustomFields(VtdvSession session)
    {
        foreach (var item in session.Items)
        {
            EnsureItemCustomFields(item);
        }
    }

    private void EnsureItemCustomFields(VtdvItem item)
    {
        foreach (var column in ProjectInfo.VtdvCustomColumns)
        {
            if (string.IsNullOrWhiteSpace(column.Name)) continue;
            if (!item.CustomFields.Any(f => string.Equals(f.Name, column.Name, StringComparison.OrdinalIgnoreCase)))
            {
                item.CustomFields.Add(new CvxdCustomFieldValue { Name = column.Name });
            }
        }
    }

    [RelayCommand]
    private void AddVtdvCustomColumn()
    {
        if (!IsAdmin) return;

        var defaultName = $"CotVtdvMoi{ProjectInfo.VtdvCustomColumns.Count + 1}";
        var name = ColumnNameDialog.Prompt(
            "Thêm cột VTĐV",
            "Nhập tên cột mới. Tên này sẽ xuất hiện trong bảng VTĐV và dùng để mapping placeholder không cố định.",
            defaultName)?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (ProjectInfo.VtdvCustomColumns.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            CvxdMessageBox.Show("Trùng tên cột", $"Cột '{name}' đã tồn tại trong bảng VTĐV.", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ProjectInfo.VtdvCustomColumns.Add(new CvxdCustomColumn { Name = name });
        foreach (var session in Sessions)
        {
            EnsureSessionCustomFields(session);
        }

        if (!ProjectInfo.CustomPlaceholders.Any(p => string.Equals(p.Value, name, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Scope, "VTĐV", StringComparison.OrdinalIgnoreCase)))
        {
            ProjectInfo.CustomPlaceholders.Add(new CvxdCustomPlaceholder
            {
                Key = "{" + name + "}",
                Value = name,
                Scope = "VTĐV"
            });
        }

        _cvxdVM.RefreshPlaceholderConfiguration();
    }

    [RelayCommand]
    private void RenameVtdvCustomColumn(string? name)
    {
        if (!IsAdmin || string.IsNullOrWhiteSpace(name)) return;

        var column = ProjectInfo.VtdvCustomColumns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (column == null) return;

        var newName = ColumnNameDialog.Prompt(
            "Đổi tên cột VTĐV",
            $"Nhập tên mới cho cột '{name}'. Dữ liệu hiện có và placeholder mapping sẽ được giữ lại.",
            name)?.Trim();
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, name, StringComparison.OrdinalIgnoreCase)) return;

        if (ProjectInfo.VtdvCustomColumns.Any(c => !ReferenceEquals(c, column) && string.Equals(c.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            CvxdMessageBox.Show("Trùng tên cột", $"Cột '{newName}' đã tồn tại trong bảng VTĐV.", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        column.Name = newName;
        foreach (var session in Sessions)
        {
            foreach (var item in session.Items)
            {
                foreach (var field in item.CustomFields.Where(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    field.Name = newName;
                }
                EnsureItemCustomFields(item);
            }
        }

        UpdateMappedPlaceholdersForColumnRename(name, newName);
        _cvxdVM.IsDirty = true;
        _cvxdVM.RefreshPlaceholderConfiguration();
    }

    [RelayCommand]
    private void DeleteVtdvCustomColumn(string? name)
    {
        if (!IsAdmin || string.IsNullOrWhiteSpace(name)) return;

        var column = ProjectInfo.VtdvCustomColumns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (column == null) return;

        var result = CvxdMessageBox.Show("Xóa cột", $"Xóa cột '{name}' và dữ liệu tương ứng trong các dòng vật tư VTĐV?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        ProjectInfo.VtdvCustomColumns.Remove(column);
        foreach (var session in Sessions)
        {
            foreach (var item in session.Items)
            {
                for (int i = item.CustomFields.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(item.CustomFields[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        item.CustomFields.RemoveAt(i);
                    }
                }
            }
        }

        for (int i = ProjectInfo.CustomPlaceholders.Count - 1; i >= 0; i--)
        {
            var placeholder = ProjectInfo.CustomPlaceholders[i];
            if (string.Equals(placeholder.Value, name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(placeholder.Scope, "VTĐV", StringComparison.OrdinalIgnoreCase))
            {
                ProjectInfo.CustomPlaceholders.RemoveAt(i);
            }
        }

        _cvxdVM.IsDirty = true;
        _cvxdVM.RefreshPlaceholderConfiguration();
    }

    private void UpdateMappedPlaceholdersForColumnRename(string oldName, string newName)
    {
        foreach (var placeholder in ProjectInfo.CustomPlaceholders)
        {
            if (!string.Equals(placeholder.Value, oldName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(placeholder.Scope, "VTĐV", StringComparison.OrdinalIgnoreCase)) continue;

            placeholder.Value = newName;
            if (string.Equals(placeholder.Key, "{" + oldName + "}", StringComparison.OrdinalIgnoreCase))
            {
                placeholder.Key = "{" + newName + "}";
            }
        }
    }

    [RelayCommand]
    private async Task ExportWord()
    {
        if (SelectedDocument == null)
        {
            MessageBox.Show("Vui lòng chọn một hồ sơ/hợp đồng trước khi xuất Word VTĐV.",
                "Chưa chọn hồ sơ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Sessions.Count == 0)
        {
            MessageBox.Show("Chưa có biên bản VTĐV để xuất. Vui lòng nhập hoặc import dữ liệu trước.",
                "Chưa có thông tin VTĐV", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedSessions = Sessions.Where(s => s.IsSelected).ToList();
        
        // Nếu không có cái nào được check, thì lấy cái đang được select (click)
        if (selectedSessions.Count == 0 && SelectedSession != null)
        {
            selectedSessions.Add(SelectedSession);
        }

        if (selectedSessions.Count == 0)
        {
            MessageBox.Show("Vui lòng chọn ít nhất một biên bản VTĐV trước khi xuất Word.",
                "Chưa chọn biên bản", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var templatePath = ResolveTemplatePath();
        if (string.IsNullOrEmpty(templatePath))
        {
            _mainVM.ShowToast("Không tìm thấy file VTDV_Template.docx trong thư mục VTĐV.", false);
            return;
        }

        foreach (var session in selectedSessions)
        {
            session.DuAn = ProjectInfo.TenDuAn;
            session.CongTrinh = ProjectInfo.TenCongTrinh;
            session.HangMuc = ProjectInfo.TenHangMuc;
            session.NhaThau = ProjectInfo.TenNhaThau;
            session.DiaDiem = ProjectInfo.DiaDiem;
        }

        var preflight = _exportService.ValidateExport(GlobalConfig, selectedSessions, templatePath, ProjectInfo.TemplateProfile);
        if (!preflight.CanExport)
        {
            CvxdMessageBox.Show("Kiểm tra trước khi xuất", preflight.ToMessage(),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _cvxdVM.IsBusy = true;
        _cvxdVM.StatusMessage = "Đang khởi tạo tiến trình xuất Word VTĐV...";
        _cvxdVM.ProgressValue = 0;
        var progress = new Progress<CvxdExportProgress>(state =>
        {
            _cvxdVM.StatusMessage = state.Message;
            _cvxdVM.ProgressValue = state.Percent;
        });
        try
        {
            var checkedTemplatePath = ResolveTemplatePath();
            if (string.IsNullOrEmpty(checkedTemplatePath))
            {
                _mainVM.ShowToast("Không tìm thấy file VTDV_Template.docx trong thư mục VTĐV.", false);
                return;
            }

            string resultPath;
            if (selectedSessions.Count == 1)
            {
                var session = selectedSessions[0];
                session.DuAn = ProjectInfo.TenDuAn;
                session.CongTrinh = ProjectInfo.TenCongTrinh;
                session.HangMuc = ProjectInfo.TenHangMuc;
                session.NhaThau = ProjectInfo.TenNhaThau;
                session.DiaDiem = ProjectInfo.DiaDiem;
                resultPath = await _exportService.ExportToWordAsync(SelectedDocument, session, ProjectInfo, GlobalConfig, templatePath, progress);
            }
            else
            {
                foreach (var session in selectedSessions)
                {
                    session.DuAn = ProjectInfo.TenDuAn;
                    session.CongTrinh = ProjectInfo.TenCongTrinh;
                    session.HangMuc = ProjectInfo.TenHangMuc;
                    session.NhaThau = ProjectInfo.TenNhaThau;
                    session.DiaDiem = ProjectInfo.DiaDiem;
                }
                resultPath = await _exportService.ExportAllToWordAsync(SelectedDocument, selectedSessions, ProjectInfo, GlobalConfig, templatePath, progress);
            }

            if (!string.IsNullOrEmpty(resultPath))
            {
                CvxdMessageBox.Show("Thông báo", "Đã xuất biên bản thành công!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
             CvxdMessageBox.Show("Lỗi", $"Lỗi khi xuất file: {ex.Message}", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            foreach (var session in selectedSessions)
            {
                session.DuAn = ProjectInfo.TenDuAn;
                session.CongTrinh = ProjectInfo.TenCongTrinh;
                session.HangMuc = ProjectInfo.TenHangMuc;
                session.NhaThau = ProjectInfo.TenNhaThau;
                session.DiaDiem = ProjectInfo.DiaDiem;
            }
            _cvxdVM.IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleSelectAll(bool? isChecked)
    {
        bool select = isChecked ?? false;
        foreach (var s in Sessions)
        {
            s.IsSelected = select;
        }
    }

    [RelayCommand]
    private void BrowseTemplatePath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Chọn file Word mẫu VTĐV",
            Filter = "Word Documents (*.docx)|*.docx|All Files (*.*)|*.*",
            Multiselect = false
        };
        
        if (dialog.ShowDialog() == true)
        {
            TemplateFilePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private async Task ImportExcel()
    {
        await _cvxdVM.ImportVtdvExcelCommand.ExecuteAsync(null);
    }

    private string? ResolveTemplatePath()
    {
        // Ưu tiên lấy từ GlobalConfig
        string parentRoot = TemplateProfileResolver.GetTemplateRoot(_cvxdVM.GlobalConfig, ProjectInfo?.TemplateProfile);

        string vtdvFolder = TemplateProfileResolver.GetVtdvFolder(parentRoot);
        string templateFile = Path.Combine(vtdvFolder, "VTDV_Template.docx");

        if (File.Exists(templateFile))
            return templateFile;

        // Thử tìm file có tên gần giống trong thư mục VTĐV
        if (Directory.Exists(vtdvFolder))
        {
            var found = Directory.GetFiles(vtdvFolder, "*Template*.docx", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (found != null) return found;
        }

        return null;
    }

    [RelayCommand]
    private void SaveData()
    {
        try
        {
            SaveVtdvDataOnly();
            _cvxdVM.SaveDataCommand.Execute(null);

            _mainVM.ShowToast($"Đã lưu dữ liệu VTĐV: {Sessions.Count} biên bản, {Sessions.Sum(s => s.Items.Count)} dòng vật tư.", true);
        }
        catch (Exception ex)
        {
            _mainVM.ShowToast($"Lỗi khi lưu dữ liệu: {ex.Message}", false);
        }
    }

    public void SaveVtdvDataOnly()
    {
        if (SelectedDocument == null) return;

        var dataPath = GetVtdvDataPath(SelectedDocument);
        if (string.IsNullOrEmpty(dataPath)) return;

        var folder = Path.GetDirectoryName(dataPath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string json = JsonSerializer.Serialize(Sessions.ToList(), _jsonOptions);
        File.WriteAllText(dataPath, json);
    }

    public void AppendImportedSessions(IEnumerable<VtdvSession> importedSessions)
    {
        RunWithoutDirtyTracking(() =>
        {
            foreach (var session in importedSessions)
            {
                EnsureSessionCustomFields(session);
                RenumberItems(session);
                Sessions.Add(session);
            }

            UpdateSessionNumbers();
            SelectedSession = Sessions.LastOrDefault();
            SelectedVtdvItem = SelectedSession?.Items.FirstOrDefault();
        });

        _cvxdVM.IsDirty = true;
    }

    public void ReplaceImportedSessions(IEnumerable<VtdvSession> importedSessions)
    {
        RunWithoutDirtyTracking(() =>
        {
            Sessions.Clear();
            foreach (var session in importedSessions)
            {
                EnsureSessionCustomFields(session);
                RenumberItems(session);
                Sessions.Add(session);
            }

            UpdateSessionNumbers();
            SelectedSession = Sessions.FirstOrDefault();
            SelectedVtdvItem = SelectedSession?.Items.FirstOrDefault();
        });

        _cvxdVM.IsDirty = true;
    }

    public void NotifyProjectInfoChanged()
    {
        OnPropertyChanged(nameof(ProjectInfo));
    }

    public void RefreshContext(Document? forceDoc = null)
    {
        Documents = new ObservableCollection<Document>(_mainVM.Documents);
        
        // Ưu tiên sử dụng forceDoc nếu có (từ CvxdVM), nếu không lấy từ MainVM
        var currentDoc = forceDoc ?? _mainVM.SelectedDocument;
        
        if (SelectedDocument != currentDoc)
        {
            RunWithoutDirtyTracking(() =>
            {
                SelectedDocument = currentDoc;
            });
        }
        else if (currentDoc != null)
        {
            // Trường hợp instance giống nhau nhưng cần reload
            LoadVtdvData(currentDoc);
        }
        
        OnPropertyChanged(nameof(ProjectInfo));
    }

    private void RunWithoutDirtyTracking(Action action)
    {
        var previous = _suppressDirtyTracking;
        _suppressDirtyTracking = true;
        try
        {
            action();
        }
        finally
        {
            _suppressDirtyTracking = previous;
        }
    }
}
