using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DocumentManagement.Services;
using DocumentManagement.Utilities;

namespace DocumentManagement.Views;

public partial class ExportedFilesWindow : Window
{
    private readonly string _root;
    private List<ExportedFile> _files = [];
    private List<ExportedFile> _filteredFiles = [];
    private bool _loading;
    private int _page = 1;
    private int _pageSize = 10;

    public ExportedFilesWindow(string contractNumber, string folder)
    {
        _root = Path.GetFullPath(folder);
        InitializeComponent();
        AppThemeResources.Apply(Resources, AppThemeResources.LoadSavedIsDarkTheme());
        SearchBox.ToolTip = "Tìm theo tên file, mã hồ sơ hoặc đường dẫn";
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_loading) return;
        _loading = true;
        StatusLabel.Text = "Đang đọc danh sách hồ sơ...";
        try
        {
            _files = await Task.Run(Scan);
            _page = 1;
            ApplyFilter();
            StatusLabel.Text = string.Empty;
        }
        catch (Exception ex) { StatusLabel.Text = $"Không thể đọc danh sách: {ex.Message}"; }
        finally { _loading = false; }
    }

    private List<ExportedFile> Scan()
    {
        var entries = ExportedFileRegistry.Read(_root);
        var known = entries.Select(e => e.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var exportRoot = Path.Combine(_root, "3.HSCL");

        if (Directory.Exists(exportRoot))
        {
            foreach (var path in Directory.EnumerateFiles(exportRoot, "*.*", SearchOption.AllDirectories)
                         .Where(IsSupportedExport))
            {
                var relative = Path.GetRelativePath(_root, path);
                if (known.Contains(relative)) continue;

                var category = ExportedDocumentMetadata.TryRead(path, out var metadataCategory)
                    ? metadataCategory
                    : InferCategory(path);
                var created = File.GetCreationTime(path);
                entries.Add(new ExportedFileEntry(relative, category, created));
                known.Add(relative);
            }
        }

        return entries.OrderByDescending(e => e.Created).Select((entry, index) =>
        {
            var path = Path.GetFullPath(Path.Combine(_root, entry.Path));
            return new ExportedFile(index + 1, path, Path.GetFileName(path), entry.Category,
                entry.Path, entry.Created, File.Exists(path), entry);
        }).ToList();
    }

    private static bool IsSupportedExport(string path)
    {
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return false;

        // Only recover files that follow the app's export naming convention.
        // This prevents ordinary reference/template files in 3.HSCL from being listed.
        var name = Path.GetFileName(path);
        return name.StartsWith("CVXD_", StringComparison.OrdinalIgnoreCase) ||
               (Path.GetDirectoryName(path)?.EndsWith("PDVT", StringComparison.OrdinalIgnoreCase) == true &&
                name.StartsWith("PDVT ", StringComparison.OrdinalIgnoreCase)) ||
               name.StartsWith("VTDV_", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("HSPL_", StringComparison.OrdinalIgnoreCase) ||
               ExportedDocumentMetadata.TryRead(path, out _);
    }

    private static string InferCategory(string path)
    {
        var normalized = path.Replace('/', '\\');
        if (normalized.Contains("\\PDVT\\", StringComparison.OrdinalIgnoreCase)) return "PDVT";
        if (normalized.Contains("\\VTĐV\\", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(path).StartsWith("VTDV_", StringComparison.OrdinalIgnoreCase))
            return "VTĐV";
        if (normalized.Contains("\\HSPL\\", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(path).StartsWith("HSPL_", StringComparison.OrdinalIgnoreCase))
            return "HSPL";
        return "CVXD";
    }

    private void ApplyFilter()
    {
        if (FilesList == null || CategoryBox == null || StatusBox == null || SortBox == null) return;
        var category = ItemText(CategoryBox);
        var status = ItemText(StatusBox);
        var sort = ItemText(SortBox);
        var query = SearchBox.Text.Trim();

        IEnumerable<ExportedFile> result = _files.Where(file =>
            (category == "Tất cả loại hồ sơ" || file.Category == category) &&
            (status == "Tất cả trạng thái" || status == "Đã tạo" && file.Exists || status == "Không tìm thấy" && !file.Exists) &&
            (string.IsNullOrEmpty(query) ||
             file.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
             file.Code.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
             file.Path.Contains(query, StringComparison.CurrentCultureIgnoreCase)));

        result = sort switch
        {
            "Cũ nhất" => result.OrderBy(file => file.Created),
            "Tên A–Z" => result.OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => result.OrderByDescending(file => file.Created)
        };

        _filteredFiles = result.ToList();
        var pageCount = Math.Max(1, (int)Math.Ceiling(_filteredFiles.Count / (double)_pageSize));
        _page = Math.Clamp(_page, 1, pageCount);
        var pageFiles = _filteredFiles.Skip((_page - 1) * _pageSize).Take(_pageSize).ToList();
        for (var index = 0; index < pageFiles.Count; index++)
            pageFiles[index].Number = (_page - 1) * _pageSize + index + 1;

        FilesList.ItemsSource = pageFiles;
        EmptyPanel.Visibility = pageFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_files.Count == 0)
        {
            EmptyTitle.Text = "Chưa có hồ sơ nào được tạo";
            EmptySubtitle.Text = "Các hồ sơ đã xuất sẽ xuất hiện tại đây";
        }
        else
        {
            EmptyTitle.Text = "Không tìm thấy hồ sơ phù hợp";
            EmptySubtitle.Text = "Hãy thử thay đổi từ khóa hoặc bộ lọc";
        }
        TotalLabel.Text = $"Tổng cộng: {_filteredFiles.Count} hồ sơ";
        PageLabel.Text = _page.ToString();
        PreviousButton.IsEnabled = _page > 1;
        NextButton.IsEnabled = _page < pageCount;
        if (!_loading) StatusLabel.Text = string.Empty;
    }

    private static string ItemText(ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;

    private void FilterChanged(object sender, TextChangedEventArgs e) { _page = 1; ApplyFilter(); }
    private void CategoryChanged(object sender, SelectionChangedEventArgs e) { _page = 1; ApplyFilter(); }
    private void StatusChanged(object sender, SelectionChangedEventArgs e) { _page = 1; ApplyFilter(); }
    private void SortChanged(object sender, SelectionChangedEventArgs e) { _page = 1; ApplyFilter(); }

    private void PageSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if ((PageSizeBox?.SelectedItem as ComboBoxItem)?.Tag is string size && int.TryParse(size, out var value))
            _pageSize = value;
        _page = 1;
        ApplyFilter();
    }

    private void PreviousPageClick(object sender, RoutedEventArgs e)
    {
        if (_page > 1) { _page--; ApplyFilter(); }
    }

    private void NextPageClick(object sender, RoutedEventArgs e)
    {
        var pageCount = Math.Max(1, (int)Math.Ceiling(_filteredFiles.Count / (double)_pageSize));
        if (_page < pageCount) { _page++; ApplyFilter(); }
    }

    private void ComboBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ComboBox combo || combo.IsDropDownOpen) return;
        combo.Focus();
        combo.IsDropDownOpen = true;
        e.Handled = true;
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not Button) DragMove();
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void ActionButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu == null) return;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        button.ContextMenu.HorizontalOffset = -152;
        button.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void FilesListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (FindAncestor<DataGridRow>(source) is { Item: ExportedFile file } && file.Exists) OpenPath(file.Path, false);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T result) return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static ExportedFile? GetFile(object sender) => (sender as FrameworkElement)?.DataContext as ExportedFile;

    private void OpenFileClick(object sender, RoutedEventArgs e)
    {
        if (GetFile(sender) is { } file) OpenPath(file.Path, false);
    }

    private void RevealFileClick(object sender, RoutedEventArgs e)
    {
        if (GetFile(sender) is { } file) OpenPath(file.Path, true);
    }

    private void CopyPathClick(object sender, RoutedEventArgs e)
    {
        if (GetFile(sender) is not { } file) return;
        try { Clipboard.SetText(file.Path); StatusLabel.Text = "Đã sao chép đường dẫn."; }
        catch (Exception ex) { StatusLabel.Text = $"Không thể sao chép: {ex.Message}"; }
    }

    private void OpenPath(string path, bool reveal)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                StatusLabel.Text = "File hoặc thư mục đã bị di chuyển hoặc xóa.";
                return;
            }
            if (reveal) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            else Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { StatusLabel.Text = $"Không thể mở: {ex.Message}"; }
    }

    private async void RenameFileClick(object sender, RoutedEventArgs e)
    {
        if (GetFile(sender) is not { } file) return;
        var brush = TryFindResource("Blueprint.Surface") as SolidColorBrush;
        var dark = brush != null && brush.Color.R < 128;
        var dialog = new RenameDialog(Path.GetFileNameWithoutExtension(file.Name), "hồ sơ (giữ nguyên định dạng)", dark) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            ExportedFileRegistry.Rename(_root, file.Entry, dialog.NewName);
            await RefreshAsync();
        }
        catch (Exception ex) { StatusLabel.Text = $"Không thể đổi tên: {ex.Message}"; }
    }

    private sealed class ExportedFile
    {
        public ExportedFile(int number, string path, string name, string category, string relativePath,
            DateTime created, bool exists, ExportedFileEntry entry)
        {
            Number = number;
            Path = path;
            Name = name;
            Category = category;
            RelativePath = relativePath;
            Created = created;
            Exists = exists;
            Entry = entry;
        }

        public int Number { get; set; }
        public string Path { get; }
        public string Name { get; }
        public string Category { get; }
        public string RelativePath { get; }
        public DateTime Created { get; }
        public bool Exists { get; }
        public ExportedFileEntry Entry { get; }
        public string Code => Category;
        public string FormatDisplay => System.IO.Path.GetExtension(Name).ToLowerInvariant();
        public string FormatLetter => FormatDisplay switch
        {
            ".doc" or ".docx" => "W",
            ".xls" or ".xlsx" => "X",
            ".pdf" => "P",
            _ => "F"
        };
    }
}
