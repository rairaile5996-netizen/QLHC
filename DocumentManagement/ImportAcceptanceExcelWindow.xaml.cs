using System;
using System.IO;
using System.Windows;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace DocumentManagement;

public enum AcceptanceExcelImportTarget
{
    Cvxd,
    Vtdv
}

public partial class ImportAcceptanceExcelWindow : Window
{
    private readonly string _baseFolderPath;
    private readonly string _templatePath;
    private readonly AcceptanceExcelImportTarget _target;

    public ImportAcceptanceExcelWindow(string baseFolderPath, AcceptanceExcelImportTarget target)
    {
        InitializeComponent();
        _baseFolderPath = Directory.Exists(baseFolderPath) ? baseFolderPath : AppDomain.CurrentDomain.BaseDirectory;
        _target = target;

        var isCvxd = target == AcceptanceExcelImportTarget.Cvxd;
        _templatePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "ImportTemplates",
            isCvxd ? "CVXD_Import_Template.xlsx" : "VTDV_Import_Template.xlsx");

        Title = isCvxd ? "Import Excel CVXD" : "Import Excel VTĐV";
        TitleText.Text = Title;
        CvxdGuidePanel.Visibility = isCvxd ? Visibility.Visible : Visibility.Collapsed;
        VtdvGuidePanel.Visibility = isCvxd ? Visibility.Collapsed : Visibility.Visible;
        DownloadTemplateText.Text = isCvxd ? "Tải file mẫu CVXD" : "Tải file mẫu VTĐV";
    }

    public string? SelectedFilePath { get; private set; }

    private void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        var documentName = _target == AcceptanceExcelImportTarget.Cvxd ? "CVXD" : "VTĐV";
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Excel Workbook (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*",
            Title = $"Chọn file Excel để import {documentName}",
            InitialDirectory = _baseFolderPath,
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        SelectedFilePath = openFileDialog.FileName;
        SelectedFileText.Text = SelectedFilePath;
        StatusText.Text = "File đã sẵn sàng để import.";
        ImportButton.IsEnabled = true;
    }

    private void DownloadTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureTemplateAvailable();

        if (!File.Exists(_templatePath))
        {
            MessageBox.Show("Không tìm thấy file template import đi kèm app.", "Thiếu template", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var fileName = Path.GetFileName(_templatePath);
        var saveDialog = new SaveFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = fileName,
            Title = "Lưu file mẫu import",
            InitialDirectory = _baseFolderPath
        };

        if (saveDialog.ShowDialog() != true)
        {
            return;
        }

        File.Copy(_templatePath, saveDialog.FileName, true);
        StatusText.Text = $"Đã lưu file mẫu tại: {saveDialog.FileName}";
    }

    private void EnsureTemplateAvailable()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_templatePath)!);

        using var workbook = new XLWorkbook();
        if (_target == AcceptanceExcelImportTarget.Cvxd)
        {
            CreateCvxdTemplate(workbook);
        }
        else
        {
            CreateVtdvTemplate(workbook);
        }

        workbook.SaveAs(_templatePath);
    }

    private static void CreateCvxdTemplate(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add("CVXD");
        var headers = new[] { "STT", "Loại NT", "Nội dung nghiệm thu", "Ngày NT", "Bắt đầu", "Kết thúc" };
        var sample = new object[] { 1, "CVXD", "Nghiệm thu ống cấp nước", "23/07/2026", "09h00", "10h00" };
        CreateTemplateSheet(sheet, headers, sample, new[] { 8d, 14d, 34d, 14d, 12d, 12d });
    }

    private static void CreateVtdvTemplate(XLWorkbook workbook)
    {
        var headers = new[] { "Bắt đầu", "Kết thúc", "Địa điểm", "STT", "Tên vật tư đối tượng", "Số phiếu xuất", "Số lượng", "Đơn vị", "Thương hiệu xuất xứ", "Ghi chú" };
        var sample = new object[] { "08h00", "10h00", "Tại công trường", 1, "Ống thép mạ kẽm", "PX-01", 10, "m", string.Empty, string.Empty };
        var widths = new[] { 12d, 12d, 22d, 8d, 30d, 16d, 12d, 12d, 24d, 20d };

        foreach (var name in new[] { "01", "02", "03" })
        {
            CreateTemplateSheet(workbook.Worksheets.Add(name), headers, name == "01" ? sample : new object[headers.Length], widths);
        }
    }

    private static void CreateTemplateSheet(IXLWorksheet sheet, IReadOnlyList<string> headers, IReadOnlyList<object> sample, IReadOnlyList<double> widths)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
            sheet.Cell(2, index + 1).Value = sample[index]?.ToString() ?? string.Empty;
            sheet.Column(index + 1).Width = widths[index];
        }

        var headerRange = sheet.Range(1, 1, 1, headers.Count);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("1F4E78");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var dataRange = sheet.Range(2, 1, 2, headers.Count);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Row(1).Height = 32;
        sheet.Row(2).Height = 22;
        sheet.SheetView.FreezeRows(1);
        sheet.ShowGridLines = false;
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath) || !File.Exists(SelectedFilePath))
        {
            MessageBox.Show("Vui lòng chọn file Excel hợp lệ trước khi import.", "Chưa chọn file", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
