using ClosedXML.Excel;
using Microsoft.Win32;
using DocumentManagement.Utilities;

namespace DocumentManagement.Views;

public partial class WorkerImportGuideWindow : System.Windows.Window
{
    public string? SelectedFilePath { get; private set; }

    public WorkerImportGuideWindow()
    {
        InitializeComponent();
        AppThemeResources.Apply(Resources, AppThemeResources.LoadSavedIsDarkTheme());
    }

    private void ChooseFile_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Tệp Excel|*.xlsx;*.xls|Tất cả tệp|*.*",
            Title = "Chọn danh sách công nhân Excel"
        };
        if (dialog.ShowDialog() != true) return;
        SelectedFilePath = dialog.FileName;
        DialogResult = true;
    }

    private void DownloadTemplate_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Tệp Excel|*.xlsx",
            FileName = "Mau_danh_sach_cong_nhan.xlsx",
            Title = "Lưu file mẫu danh sách công nhân"
        };
        if (dialog.ShowDialog() != true) return;

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Danh sách công nhân");
        sheet.Cell(1, 1).Value = "Họ và tên";
        sheet.Cell(1, 2).Value = "Chức vụ / Nghề";
        sheet.Cell(1, 3).Value = "Ngày sinh";
        sheet.Cell(2, 1).Value = "Nguyễn Văn A";
        sheet.Cell(2, 2).Value = "Thợ điện";
        sheet.Cell(2, 3).Value = new DateTime(1995, 5, 12);
        sheet.Range("A1:C1").Style.Font.Bold = true;
        sheet.Range("A1:C1").Style.Fill.BackgroundColor = XLColor.FromHtml("1F2F49");
        sheet.Range("A1:C1").Style.Font.FontColor = XLColor.White;
        sheet.Column(1).Width = 28;
        sheet.Column(2).Width = 24;
        sheet.Column(3).Width = 16;
        sheet.Column(3).Style.DateFormat.Format = "dd/MM/yyyy";
        sheet.SheetView.FreezeRows(1);
        workbook.SaveAs(dialog.FileName);
    }

    private void Close_Click(object sender, System.Windows.RoutedEventArgs e) => Close();
}
