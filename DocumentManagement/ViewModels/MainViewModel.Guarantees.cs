using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentManagement.Models;
using DocumentManagement.Services;
using DocumentManagement.Views;

namespace DocumentManagement.ViewModels;

public enum GuaranteeListRowKind
{
    RelationHeader = 0,
    ChainHeader = 1,
    Record = 2
}

public partial class GuaranteeListRow : ObservableObject
{
    [ObservableProperty]
    private bool _isHighlighted;

    public GuaranteeListRowKind Kind { get; init; }
    public int GuaranteeId { get; init; }
    public GuaranteeLetter? Guarantee { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string RelationLabel { get; init; } = string.Empty;
    public string TypeDisplay { get; init; } = string.Empty;
    public string LifecycleDisplay { get; init; } = string.Empty;
    public string StatusDisplay { get; init; } = string.Empty;
    public string LetterNumber { get; init; } = string.Empty;
    public string BankDisplay { get; init; } = string.Empty;
    public string ValueDisplay { get; init; } = string.Empty;
    public string DateDisplay { get; init; } = string.Empty;
    public string PdfStatusText { get; init; } = string.Empty;
    public string AmendmentBadgeText { get; init; } = string.Empty;
    public Thickness IndentMargin { get; init; } = new(0);
    public GuaranteeAlertSeverity Severity { get; init; }
    public bool IsPdfMissing { get; init; }
    public bool CanResolveLifecycle { get; init; }
    public bool CanAmend { get; init; }
    public bool HasPdf { get; init; }
    public bool HasAmendments { get; init; }
    public bool IsExpanded { get; init; }
    public string CurrentLetterNumber { get; init; } = string.Empty;
    public string CurrentDateDisplay { get; init; } = string.Empty;
    public string CurrentStatusDisplay { get; init; } = string.Empty;
    public GuaranteeAlertSeverity CurrentSeverity { get; init; }
    public bool IsRecord => Kind == GuaranteeListRowKind.Record;
}

public partial class MainViewModel
{
    private readonly IGuaranteeService _guaranteeService;
    private int _guaranteeLoadVersion;
    private IReadOnlyList<GuaranteeLetter> _loadedGuarantees = [];
    private readonly Dictionary<int, bool> _expandedGuaranteeChains = [];

    [ObservableProperty]
    private ObservableCollection<GuaranteeListRow> _guaranteeRows = [];

    [ObservableProperty]
    private GuaranteeListRow? _selectedGuaranteeRow;

    [ObservableProperty]
    private ObservableCollection<GuaranteeAlertItem> _guaranteeAlerts = [];

    [ObservableProperty]
    private bool _isGuaranteeNotificationOpen;

    [ObservableProperty]
    private int _guaranteeAlertCount;

    [ObservableProperty]
    private bool _isGuaranteeBusy;

    public bool HasGuaranteeRecords => GuaranteeRows.Any(row =>
        row.Kind is GuaranteeListRowKind.ChainHeader or GuaranteeListRowKind.Record);
    public bool HasGuaranteeAlerts => GuaranteeAlertCount > 0;
    public GuaranteeLetter? SelectedGuarantee => SelectedGuaranteeRow?.Guarantee;
    public bool CanEditSelectedGuarantee => SelectedGuarantee != null;
    public bool CanOpenSelectedGuaranteePdf => SelectedGuaranteeRow?.HasPdf == true;
    public bool CanAmendSelectedGuarantee => SelectedGuaranteeRow?.Guarantee != null;
    public bool CanResolveSelectedGuarantee => SelectedGuaranteeRow?.CanResolveLifecycle == true;
    public bool CanDeleteSelectedGuarantee => SelectedGuaranteeRow?.Guarantee != null;

    partial void OnGuaranteeRowsChanged(ObservableCollection<GuaranteeListRow> value)
    {
        OnPropertyChanged(nameof(HasGuaranteeRecords));
    }

    partial void OnSelectedGuaranteeRowChanged(GuaranteeListRow? value)
    {
        foreach (var row in GuaranteeRows.Where(row => row.IsRecord))
        {
            row.IsHighlighted = false;
        }

        if (value?.IsRecord == true)
        {
            value.IsHighlighted = true;
        }

        OnPropertyChanged(nameof(SelectedGuarantee));
        OnPropertyChanged(nameof(CanEditSelectedGuarantee));
        OnPropertyChanged(nameof(CanOpenSelectedGuaranteePdf));
        OnPropertyChanged(nameof(CanAmendSelectedGuarantee));
        OnPropertyChanged(nameof(CanResolveSelectedGuarantee));
        OnPropertyChanged(nameof(CanDeleteSelectedGuarantee));
    }

    public async Task RefreshGuaranteeAlertDataAsync(CancellationToken cancellationToken = default)
    {
        var alerts = await _guaranteeService.GetActiveAlertsAsync(cancellationToken);
        GuaranteeAlerts = new ObservableCollection<GuaranteeAlertItem>(alerts);
        GuaranteeAlertCount = GuaranteeAlerts.Count;
        OnPropertyChanged(nameof(HasGuaranteeAlerts));
    }

    public async Task LoadGuaranteesForSelectedDocumentAsync(
        int? preferredGuaranteeId = null,
        CancellationToken cancellationToken = default)
    {
        var document = SelectedDocument;
        if (document == null)
        {
            ClearGuaranteeRows();
            return;
        }

        var loadVersion = Interlocked.Increment(ref _guaranteeLoadVersion);
        IsGuaranteeBusy = true;

        try
        {
            var guarantees = await _guaranteeService.GetGuaranteesForDocumentAsync(document.Id, cancellationToken);
            if (loadVersion != _guaranteeLoadVersion || SelectedDocument?.Id != document.Id)
            {
                return;
            }

            var previousId = preferredGuaranteeId ?? SelectedGuaranteeRow?.GuaranteeId;
            _loadedGuarantees = guarantees;
            GuaranteeRows = new ObservableCollection<GuaranteeListRow>(BuildGuaranteeRows(guarantees));
            SelectedGuaranteeRow = GuaranteeRows.FirstOrDefault(row => row.IsRecord && row.GuaranteeId == previousId);
        }
        finally
        {
            if (loadVersion == _guaranteeLoadVersion)
            {
                IsGuaranteeBusy = false;
            }
        }
    }

    public async Task<bool> NavigateToGuaranteeAsync(
        int documentId,
        int guaranteeId,
        CancellationToken cancellationToken = default)
    {
        var targetDocument = Documents.FirstOrDefault(document => document.Id == documentId);
        if (targetDocument == null)
        {
            await RefreshGuaranteeAlertDataAsync(cancellationToken);
            return false;
        }

        if (!ReferenceEquals(SelectedDocument, targetDocument))
        {
            await SelectDocument(targetDocument);
        }

        DetailTabIndex = 5;
        await LoadGuaranteesForSelectedDocumentAsync(guaranteeId, cancellationToken);

        var targetRow = GuaranteeRows.FirstOrDefault(row => row.IsRecord && row.GuaranteeId == guaranteeId);
        if (targetRow == null)
        {
            await RefreshGuaranteeAlertDataAsync(cancellationToken);
            ShowToast("Không còn tìm thấy bảo lãnh được cảnh báo. Danh sách đã được làm mới.", false);
            return false;
        }

        SelectedGuaranteeRow = targetRow;
        IsGuaranteeNotificationOpen = false;
        return true;
    }

    [RelayCommand]
    private void ToggleGuaranteeNotifications()
    {
        IsGuaranteeNotificationOpen = !IsGuaranteeNotificationOpen;
    }

    [RelayCommand]
    private void CloseGuaranteeNotificationPanel()
    {
        IsGuaranteeNotificationOpen = false;
    }

    [RelayCommand]
    private async Task OpenGuaranteeAlert(GuaranteeAlertItem? alert)
    {
        if (alert == null)
        {
            return;
        }

        await NavigateToGuaranteeAsync(alert.DocumentId, alert.GuaranteeId);
    }

    [RelayCommand]
    private void SelectGuaranteeRow(GuaranteeListRow? row)
    {
        if (row?.IsRecord == true)
        {
            SelectedGuaranteeRow = row;
        }
    }

    [RelayCommand]
    private async Task OpenAddGuarantee()
    {
        if (SelectedDocument == null)
        {
            ShowGuaranteeError("Vui lòng chọn hồ sơ trước khi thêm bảo lãnh.");
            return;
        }

        var window = CreateGuaranteeEditorWindow(null, isAmendment: false);
        if (window.ShowDialog() != true || window.ResultRequest == null)
        {
            return;
        }

        try
        {
            var created = await _guaranteeService.CreateGuaranteeAsync(window.ResultRequest);
            await RefreshGuaranteeAfterMutationAsync(created.Id);
            DetailTabIndex = 5;
            ShowToast("Đã thêm thư bảo lãnh.", true);
        }
        catch (Exception ex)
        {
            ShowGuaranteeError($"Không thể thêm bảo lãnh:\n\n{ex.Message}");
        }
    }

    private static void ShowGuaranteeError(string message)
    {
        MessageBox.Show(
            message,
            "Không thể thêm bảo lãnh",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    [RelayCommand]
    private async Task EditGuarantee(GuaranteeListRow? row)
    {
        var targetRow = ResolveGuaranteeRow(row);
        if (targetRow?.Guarantee == null || SelectedDocument == null)
        {
            ShowToast("Vui lòng chọn bảo lãnh cần chỉnh sửa.", false);
            return;
        }

        var window = CreateGuaranteeEditorWindow(targetRow.Guarantee, isAmendment: false);
        if (window.ShowDialog() != true || window.ResultRequest == null)
        {
            return;
        }

        try
        {
            await _guaranteeService.UpdateGuaranteeAsync(targetRow.GuaranteeId, window.ResultRequest);
            await RefreshGuaranteeAfterMutationAsync(targetRow.GuaranteeId);
            ShowToast("Đã cập nhật thư bảo lãnh.", true);
        }
        catch (Exception ex)
        {
            ShowToast($"Không thể cập nhật bảo lãnh: {ex.Message}", false);
        }
    }

    [RelayCommand]
    private async Task AmendGuarantee(GuaranteeListRow? row)
    {
        var targetRow = ResolveAmendmentSource(row);
        if (targetRow?.Guarantee == null || SelectedDocument == null)
        {
            ShowToast("Không tìm thấy thư đang hiệu lực để tạo tu chỉnh mới.", false);
            return;
        }

        var window = CreateGuaranteeEditorWindow(targetRow.Guarantee, isAmendment: true);
        if (window.ShowDialog() != true || window.ResultRequest == null)
        {
            return;
        }

        try
        {
            var amendment = await _guaranteeService.CreateAmendmentAsync(targetRow.GuaranteeId, window.ResultRequest);
            await RefreshGuaranteeAfterMutationAsync(amendment.Id);
            ShowToast("Đã tạo bản tu chỉnh bảo lãnh.", true);
        }
        catch (Exception ex)
        {
            ShowToast($"Không thể tạo tu chỉnh: {ex.Message}", false);
        }
    }

    [RelayCommand]
    private async Task ReleaseGuarantee(GuaranteeListRow? row)
    {
        await ChangeGuaranteeLifecycleAsync(row, GuaranteeLifecycleStatus.Released, "giải tỏa");
    }

    [RelayCommand]
    private async Task CancelGuarantee(GuaranteeListRow? row)
    {
        await ChangeGuaranteeLifecycleAsync(row, GuaranteeLifecycleStatus.Cancelled, "hủy");
    }

    [RelayCommand]
    private async Task DeleteGuarantee(GuaranteeListRow? row)
    {
        var targetRow = ResolveGuaranteeRow(row);
        if (targetRow?.Guarantee == null)
        {
            ShowToast("Vui lòng chọn thư bảo lãnh cần xóa.", false);
            return;
        }

        if (!ConfirmDialog.Show(
                "Xóa thư bảo lãnh",
                $"Bạn có chắc muốn xóa thư '{targetRow.LetterNumber}' và PDF đã lưu không?\n\nThao tác này không thể hoàn tác.",
                null,
                "Xóa thư",
                "Hủy"))
        {
            return;
        }

        try
        {
            await _guaranteeService.DeleteGuaranteeAsync(targetRow.GuaranteeId);
            await RefreshGuaranteeAfterMutationAsync(0);
            ShowToast("Đã xóa thư bảo lãnh.", true);
        }
        catch (Exception ex)
        {
            ShowToast($"Không thể xóa thư bảo lãnh: {ex.Message}", false);
        }
    }

    [RelayCommand]
    private async Task OpenGuaranteePdf(GuaranteeListRow? row)
    {
        var targetRow = ResolveGuaranteeRow(row);
        if (targetRow == null)
        {
            ShowToast("Vui lòng chọn bảo lãnh cần mở PDF.", false);
            return;
        }

        try
        {
            await _guaranteeService.OpenPdfAsync(targetRow.GuaranteeId);
        }
        catch (Exception ex)
        {
            ShowToast($"Không thể mở PDF: {ex.Message}", false);
        }
    }

    private async Task ChangeGuaranteeLifecycleAsync(
        GuaranteeListRow? row,
        GuaranteeLifecycleStatus lifecycleStatus,
        string actionText)
    {
        var targetRow = ResolveGuaranteeRow(row);
        if (targetRow?.Guarantee == null)
        {
            ShowToast("Vui lòng chọn bảo lãnh cần cập nhật trạng thái.", false);
            return;
        }

        if (!ConfirmDialog.Show(
                "Xác nhận",
                $"Bạn có chắc muốn {actionText} thư bảo lãnh '{targetRow.LetterNumber}'?",
                null,
                "Đồng ý",
                "Hủy"))
        {
            return;
        }

        try
        {
            await _guaranteeService.UpdateLifecycleAsync(targetRow.GuaranteeId, lifecycleStatus);
            await RefreshGuaranteeAfterMutationAsync(targetRow.GuaranteeId);
            ShowToast($"Đã cập nhật trạng thái bảo lãnh sang {actionText}.", true);
        }
        catch (Exception ex)
        {
            ShowToast($"Không thể cập nhật trạng thái: {ex.Message}", false);
        }
    }

    private AddEditGuaranteeLetterWindow CreateGuaranteeEditorWindow(GuaranteeLetter? guarantee, bool isAmendment)
    {
        if (SelectedDocument == null)
        {
            throw new InvalidOperationException("Chưa có hồ sơ được chọn.");
        }

        var relationOptions = BuildGuaranteeRelationOptions(guarantee, isAmendment);
        var selectedRelation = relationOptions.FirstOrDefault(option =>
            option.RelatedAppendixId == guarantee?.RelatedAppendixId &&
            string.Equals(option.Label, guarantee?.RelationDisplayLabel, StringComparison.Ordinal))
            ?? relationOptions.First();

        var editorModel = new GuaranteeEditorDialogModel
        {
            Title = isAmendment
                ? "Thêm thư tu chỉnh mới"
                : guarantee == null
                    ? "Thêm thư bảo lãnh"
                    : "Cập nhật thư bảo lãnh",
            SubmitText = isAmendment ? "Thêm thư tu chỉnh" : "Lưu bảo lãnh",
            RequirePdf = guarantee == null || isAmendment,
            AllowRelationChange = !isAmendment,
            RelationOptions = new ObservableCollection<GuaranteeRelationOption>(relationOptions),
            SelectedRelation = selectedRelation,
            Type = guarantee?.Type ?? GuaranteeType.AdvancePayment,
            LetterNumber = isAmendment ? string.Empty : guarantee?.LetterNumber ?? string.Empty,
            IssuingBank = guarantee?.IssuingBank ?? string.Empty,
            GuaranteeValueText = guarantee?.GuaranteeValue.ToString("N0") ?? string.Empty,
            IssueDate = guarantee?.IssueDate ?? DateTime.Today,
            EffectiveDate = guarantee?.EffectiveDate ?? DateTime.Today,
            ExpiryDate = guarantee?.ExpiryDate ?? DateTime.Today,
            Notes = guarantee?.Notes ?? string.Empty,
            ExistingPdfPath = guarantee?.PdfFilePath ?? string.Empty
        };

        if (guarantee == null && !isAmendment)
        {
            editorModel.IsTypeTaken = (relationId, type) => _loadedGuarantees.Any(item =>
                item.ParentGuaranteeId == null &&
                item.RelatedAppendixId == relationId &&
                item.Type == type &&
                item.LifecycleStatus != GuaranteeLifecycleStatus.Cancelled);
            editorModel.RefreshTypeAvailability();
        }

        return new AddEditGuaranteeLetterWindow(editorModel, SelectedDocument.Id)
        {
            Owner = Application.Current.Windows.OfType<ShellWindow>().FirstOrDefault()
        };
    }

    private List<GuaranteeRelationOption> BuildGuaranteeRelationOptions(GuaranteeLetter? guarantee, bool isAmendment)
    {
        if (SelectedDocument == null)
        {
            return [new GuaranteeRelationOption { Label = "Hợp đồng gốc" }];
        }

        var options = new List<GuaranteeRelationOption>
        {
            new() { Label = "Hợp đồng gốc" }
        };

        options.AddRange(SelectedDocument.Appendices
            .OrderBy(appendix => appendix.AppendixNumber)
            .Select(appendix => new GuaranteeRelationOption
            {
                RelatedAppendixId = appendix.Id,
                Label = $"Phụ lục {appendix.AppendixNumber}".Trim()
            }));

        if (guarantee != null &&
            !options.Any(option =>
                option.RelatedAppendixId == guarantee.RelatedAppendixId &&
                string.Equals(option.Label, guarantee.RelationDisplayLabel, StringComparison.Ordinal)))
        {
            options.Add(new GuaranteeRelationOption
            {
                RelatedAppendixId = guarantee.RelatedAppendixId,
                Label = guarantee.RelationDisplayLabel
            });
        }

        if (isAmendment && guarantee != null)
        {
            return options
                .Where(option =>
                    option.RelatedAppendixId == guarantee.RelatedAppendixId &&
                    string.Equals(option.Label, guarantee.RelationDisplayLabel, StringComparison.Ordinal))
                .ToList();
        }

        return options;
    }

    private GuaranteeListRow? ResolveGuaranteeRow(GuaranteeListRow? row)
    {
        return row?.IsRecord == true ? row : SelectedGuaranteeRow;
    }

    private GuaranteeListRow? ResolveAmendmentSource(GuaranteeListRow? row)
    {
        var selected = ResolveGuaranteeRow(row);
        if (selected?.Guarantee == null)
        {
            return null;
        }

        var records = _loadedGuarantees
            .Select(guarantee => new GuaranteeListRow
            {
                Kind = GuaranteeListRowKind.Record,
                GuaranteeId = guarantee.Id,
                Guarantee = guarantee
            })
            .ToList();
        var current = selected.Guarantee;

        while (current.LifecycleStatus != GuaranteeLifecycleStatus.Active)
        {
            var next = records
                .Where(item => item.Guarantee!.ParentGuaranteeId == current.Id)
                .OrderByDescending(item => item.Guarantee!.CreatedDate)
                .ThenByDescending(item => item.Guarantee!.Id)
                .FirstOrDefault();

            if (next?.Guarantee == null)
            {
                return null;
            }

            current = next.Guarantee;
        }

        return records.FirstOrDefault(item => item.Guarantee!.Id == current.Id);
    }

    private async Task RefreshGuaranteeAfterMutationAsync(int preferredGuaranteeId)
    {
        await LoadGuaranteesForSelectedDocumentAsync(preferredGuaranteeId);
        await RefreshGuaranteeAlertDataAsync();
        SelectedGuaranteeRow = GuaranteeRows.FirstOrDefault(row =>
            row.IsRecord && row.GuaranteeId == preferredGuaranteeId);
        DetailTabIndex = 5;
    }

    private void ClearGuaranteeRows()
    {
        _loadedGuarantees = [];
        _expandedGuaranteeChains.Clear();
        GuaranteeRows = [];
        SelectedGuaranteeRow = null;
        IsGuaranteeBusy = false;
    }

    [RelayCommand]
    private void ToggleGuaranteeChain(GuaranteeListRow? row)
    {
        if (row?.Kind != GuaranteeListRowKind.ChainHeader)
        {
            return;
        }

        var selectedId = SelectedGuaranteeRow?.GuaranteeId;
        _expandedGuaranteeChains[row.GuaranteeId] = !row.IsExpanded;
        GuaranteeRows = new ObservableCollection<GuaranteeListRow>(BuildGuaranteeRows(_loadedGuarantees));
        SelectedGuaranteeRow = GuaranteeRows.FirstOrDefault(item => item.IsRecord && item.GuaranteeId == selectedId);
    }

    private IReadOnlyList<GuaranteeListRow> BuildGuaranteeRows(IReadOnlyList<GuaranteeLetter> guarantees)
    {
        if (guarantees.Count == 0)
        {
            return [];
        }

        var rows = new List<GuaranteeListRow>();
        var childrenByParent = guarantees
            .Where(guarantee => guarantee.ParentGuaranteeId.HasValue)
            .GroupBy(guarantee => guarantee.ParentGuaranteeId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.CreatedDate).ThenBy(item => item.Id).ToList());

        var roots = guarantees
            .Where(guarantee => !guarantee.ParentGuaranteeId.HasValue || guarantees.All(parent => parent.Id != guarantee.ParentGuaranteeId.Value))
            .OrderBy(guarantee => GetRelationSortKey(guarantee.RelationDisplayLabel))
            .ThenBy(guarantee => guarantee.RelationDisplayLabel)
            .ThenBy(guarantee => guarantee.CreatedDate)
            .ThenBy(guarantee => guarantee.Id)
            .ToList();

        foreach (var relationGroup in roots.GroupBy(root => root.RelationDisplayLabel))
        {
            rows.Add(new GuaranteeListRow
            {
                Kind = GuaranteeListRowKind.RelationHeader,
                Title = relationGroup.Key,
                Subtitle = $"{relationGroup.Count()} chuỗi bảo lãnh"
            });

            foreach (var root in relationGroup)
            {
                var isExpanded = _expandedGuaranteeChains.TryGetValue(root.Id, out var expanded) && expanded;
                var current = GetCurrentGuarantee(root, childrenByParent);
                rows.Add(new GuaranteeListRow
                {
                    Kind = GuaranteeListRowKind.ChainHeader,
                    GuaranteeId = root.Id,
                    Title = root.TypeDisplay,
                    Subtitle = childrenByParent.ContainsKey(root.Id)
                        ? $"{root.LetterNumber} • {root.IssuingBank} • {childrenByParent[root.Id].Count} thư tu chỉnh"
                        : $"{root.LetterNumber} • {root.IssuingBank}",
                    HasAmendments = childrenByParent.ContainsKey(root.Id),
                    IsExpanded = isExpanded,
                    CurrentLetterNumber = current.LetterNumber,
                    CurrentDateDisplay = $"{current.EffectiveDate:dd/MM/yyyy} → {current.ExpiryDate:dd/MM/yyyy}",
                    CurrentStatusDisplay = current.DisplayStatus,
                    CurrentSeverity = current.AlertSeverity
                });

                AddGuaranteeChainRows(
                    rows,
                    root,
                    childrenByParent,
                    depth: 0,
                    includeChildren: isExpanded);
            }
        }

        return rows;
    }

    private static GuaranteeLetter GetCurrentGuarantee(
        GuaranteeLetter root,
        IReadOnlyDictionary<int, List<GuaranteeLetter>> childrenByParent)
    {
        var current = root;
        while (childrenByParent.TryGetValue(current.Id, out var children) && children.Count > 0)
        {
            current = children[^1];
        }

        return current;
    }

    private static void AddGuaranteeChainRows(
        ICollection<GuaranteeListRow> rows,
        GuaranteeLetter guarantee,
        IReadOnlyDictionary<int, List<GuaranteeLetter>> childrenByParent,
        int depth,
        bool includeChildren = true)
    {
        if (!includeChildren)
        {
            return;
        }

        var isPdfMissing = GuaranteeFileService.IsPdfFileMissing(guarantee.PdfFilePath);
        rows.Add(new GuaranteeListRow
        {
            Kind = GuaranteeListRowKind.Record,
            GuaranteeId = guarantee.Id,
            Guarantee = guarantee,
            Title = guarantee.TypeDisplay,
            Subtitle = guarantee.RelationDisplayLabel,
            RelationLabel = guarantee.RelationDisplayLabel,
            TypeDisplay = guarantee.TypeDisplay,
            LifecycleDisplay = guarantee.LifecycleDisplay,
            StatusDisplay = depth == 0 && childrenByParent.ContainsKey(guarantee.Id)
                ? "Đã tu chỉnh"
                : guarantee.DisplayStatus,
            LetterNumber = guarantee.LetterNumber,
            BankDisplay = guarantee.IssuingBank,
            ValueDisplay = $"{guarantee.GuaranteeValue:N0} đ",
            DateDisplay = $"{guarantee.EffectiveDate:dd/MM/yyyy} → {guarantee.ExpiryDate:dd/MM/yyyy}",
            PdfStatusText = string.IsNullOrWhiteSpace(guarantee.PdfFilePath)
                ? "Chưa có PDF"
                : isPdfMissing
                    ? "Thiếu PDF đã sao chép"
                    : "PDF đã lưu",
            AmendmentBadgeText = depth == 0 ? "Bản gốc" : $"Tu chỉnh {depth}",
            IndentMargin = new Thickness(depth * 24, 0, 0, 0),
            Severity = guarantee.AlertSeverity,
            IsPdfMissing = isPdfMissing,
            CanResolveLifecycle = guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active,
            CanAmend = guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active,
            HasPdf = !string.IsNullOrWhiteSpace(guarantee.PdfFilePath)
        });

        if (!childrenByParent.TryGetValue(guarantee.Id, out var children))
        {
            return;
        }

        foreach (var child in children)
        {
            AddGuaranteeChainRows(rows, child, childrenByParent, depth + 1, includeChildren: true);
        }
    }

    private static int GetRelationSortKey(string relationLabel)
    {
        if (relationLabel.StartsWith("Hợp đồng gốc", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (relationLabel.StartsWith("Phụ lục", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }
}
