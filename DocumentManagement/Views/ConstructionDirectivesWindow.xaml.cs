using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DocumentManagement.Models;
using DocumentManagement.Services;
using DocumentManagement.Utilities;
using DocumentManagement.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace DocumentManagement.Views;

public partial class ConstructionDirectivesWindow : Window
{
    private readonly ConstructionDirectiveService _service;
    private readonly ConstructionDirectivesViewModel _viewModel;
    private bool _importing;
    private bool? _compactLayout;
    public ConstructionDirectivesWindow(ConstructionDirectiveService service, ConstructionDirectivesViewModel viewModel)
    {
        InitializeComponent();
        _service = service; _viewModel = viewModel; DataContext = viewModel;
        // Populate immediately as well as at Loaded so a newly composed window has data before first layout.
        Reload();
        Loaded += (_, _) => { ClampToWorkArea(); Reload(scanExistingFiles: true); };
        Closed += (_, _) => { if (Application.Current.MainWindow == this) Application.Current.Shutdown(); };
    }
    private void Reload(bool scanExistingFiles = false)
    {
        FooterStoragePath.Text = _service.StorageRoot;
        try
        {
            ConstructionDirectiveScanResult? scan = scanExistingFiles ? _service.ScanExistingFiles() : null;
            _viewModel.Reload(_service);
            if (scan != null) _viewModel.Message = scan.ToMessage();
        }
        catch (Exception ex) { _viewModel.Message = "Không thể đọc danh sách: " + ex.Message; }
    }
    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DirectiveStoragePicker.Choose(this, _service)) Reload(scanExistingFiles: true);
    }
    private void OpenFolder_Click(object sender, RoutedEventArgs e) => DirectiveStoragePicker.Open(this, _service);
    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Multiselect = true, Title = "Chọn file scan chỉ thị", Filter = "PDF và ảnh scan|*.pdf;*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.bmp" };
        if (picker.ShowDialog(this) == true) ImportFiles(picker.FileNames);
    }
    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var scan = _service.ScanExistingFiles();
            _viewModel.Reload(_service);
            _viewModel.Message = scan.ToMessage();
            if (scan.Skipped.Count > 0)
                DirectiveMessageDialog.Show(this, "Một số file chưa được thêm", string.Join(Environment.NewLine, scan.Skipped), true);
        }
        catch (Exception ex) { _viewModel.Message = "Không thể quét thư mục: " + ex.Message; }
    }
    private void ImportFiles(IEnumerable<string> files)
    {
        if (_importing) return;
        _importing = true;
        var imported = 0; var cancelled = 0; var failures = new List<string>();
        try
        {
            foreach (var file in files)
            {
                try
                {
                    if (!ConstructionDirectiveService.IsSupported(file)) throw new InvalidDataException("Định dạng không được hỗ trợ.");
                    var item = ConstructionDirectiveService.ParseFileName(file);
                    if (!string.IsNullOrWhiteSpace(item.Number) && !string.IsNullOrWhiteSpace(item.Title))
                    {
                        _service.Import(file, item);
                        imported++;
                    }
                    else
                    {
                        var editor = new ConstructionDirectiveEditorWindow(item, metadata => _service.Import(file, metadata)) { Owner = this };
                        if (editor.ShowDialog() == true) imported++; else cancelled++;
                    }
                }
                catch (Exception ex) { failures.Add(Path.GetFileName(file) + ": " + ex.Message); }
            }
            Reload();
            _viewModel.Message = $"Đã thêm {imported} chỉ thị · Bỏ qua {cancelled} · Lỗi {failures.Count}.";
            if (failures.Count > 0) DirectiveMessageDialog.Show(this, "Một số file chưa được nhập", string.Join(Environment.NewLine, failures), true);
        }
        finally { _importing = false; }
    }
    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        EditSelected();
    }
    private void EditSelected()
    {
        if (_viewModel.SelectedDirective is not { } selected) { _viewModel.Message = "Chọn một chỉ thị để sửa thông tin."; return; }
        var editor = new ConstructionDirectiveEditorWindow(ConstructionDirectiveService.Copy(selected), metadata => _service.Update(metadata)) { Owner = this };
        if (editor.ShowDialog() == true) { Reload(); _viewModel.Message = "Đã lưu thông tin chỉ thị."; }
    }
    private void Open_Click(object sender, RoutedEventArgs e) => OpenSelected();
    private void ClearSearch_Click(object sender, RoutedEventArgs e) => _viewModel.SearchText = string.Empty;
    private void RowOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ConstructionDirective directive }) _viewModel.SelectedDirective = directive;
        OpenSelected();
    }
    private void RowEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ConstructionDirective directive }) _viewModel.SelectedDirective = directive;
        EditSelected();
    }
    private void Grid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControlContainer(source)) OpenSelected();
    }
    private static bool ItemsControlContainer(DependencyObject source)
    {
        while (source != null)
        {
            if (source is System.Windows.Controls.DataGridRow) return true;
            source = source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        }
        return false;
    }
    private void OpenSelected()
    {
        if (_viewModel.SelectedDirective is not { } selected) { _viewModel.Message = "Chọn một chỉ thị để mở file scan."; return; }
        try
        {
            var path = _service.GetFilePath(selected);
            if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy bản scan đã lưu.");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { _viewModel.Message = "Không thể mở file: " + ex.Message; }
    }
    private void Files_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !_importing && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private void Files_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files) ImportFiles(files);
        e.Handled = true;
    }
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        var window = App.ServiceProvider.GetRequiredService<ModuleSelectionWindow>();
        Application.Current.MainWindow = window; window.Show(); Close();
    }
    private void ClampToWorkArea()
    {
        var work = SystemParameters.WorkArea;
        Width = Math.Min(Width, work.Width - 32);
        Height = Math.Min(Height, work.Height - 32);
        Left = Math.Max(work.Left + 16, Math.Min(Left, work.Right - ActualWidth - 16));
        Top = Math.Max(work.Top + 16, Math.Min(Top, work.Bottom - ActualHeight - 16));
    }

    private void ConstructionDirectivesWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 760;
        if (_compactLayout == compact) return;
        _compactLayout = compact;

        

        SearchFilterPanel.RowDefinitions.Clear();
        SearchFilterPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        if (compact)
        {
            SearchFilterPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(YearFilter, 1);
            Grid.SetColumn(YearFilter, 0);
            YearFilter.Width = 190;
            YearFilter.HorizontalAlignment = HorizontalAlignment.Left;
            YearFilter.Margin = new Thickness(0, 8, 0, 0);
        }
        else
        {
            Grid.SetRow(YearFilter, 0);
            Grid.SetColumn(YearFilter, 1);
            YearFilter.Width = double.NaN;
            YearFilter.HorizontalAlignment = HorizontalAlignment.Stretch;
            YearFilter.Margin = new Thickness(14, 0, 0, 0);
        }

        Grid.SetRow(StorageFolderButton, 0);
        Grid.SetColumn(StorageFolderButton, 0);

        if (FooterLayout.Children.Count == 3)
        {
            var chooseButton = FooterLayout.Children[1] as UIElement;
            var statusPanel = FooterLayout.Children[2] as UIElement;
            if (chooseButton != null)
            {
                Grid.SetRow(chooseButton, compact ? 1 : 0);
                Grid.SetColumn(chooseButton, compact ? 0 : 1);
                if (chooseButton is FrameworkElement chooseElement)
                    chooseElement.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            }
            if (statusPanel != null)
            {
                Grid.SetRow(statusPanel, compact ? 1 : 0);
                Grid.SetColumn(statusPanel, compact ? 1 : 2);
                if (statusPanel is FrameworkElement statusElement)
                    statusElement.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }
    }
}
