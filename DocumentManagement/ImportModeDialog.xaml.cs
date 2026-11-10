using System.Windows;

namespace DocumentManagement;

public enum AcceptanceExcelImportMode
{
    Append,
    Replace
}

public partial class ImportModeDialog : Window
{
    private AcceptanceExcelImportMode? _selectedMode;

    private ImportModeDialog(AcceptanceExcelImportTarget target)
    {
        InitializeComponent();
        Utilities.AppThemeResources.Apply(Resources, Utilities.AppThemeResources.LoadSavedIsDarkTheme());

        var label = target == AcceptanceExcelImportTarget.Cvxd ? "CVXD" : "VTĐV";
        DescriptionText.Text = $"File Excel đã được đọc. Chọn cách áp dụng dữ liệu {label} vào hợp đồng hiện tại.";
        AppendDescriptionText.Text = target == AcceptanceExcelImportTarget.Cvxd
            ? "Nạp thêm: giữ các dòng CVXD hiện có và thêm các dòng trong file vào cuối danh sách."
            : "Nạp thêm: giữ các biên bản VTĐV hiện có và thêm các biên bản trong file vào danh sách.";
        ReplaceDescriptionText.Text = target == AcceptanceExcelImportTarget.Cvxd
            ? "Nạp đè: thay toàn bộ danh sách CVXD hiện có bằng dữ liệu trong file."
            : "Nạp đè: thay toàn bộ danh sách biên bản VTĐV hiện có bằng dữ liệu trong file.";
    }

    public static AcceptanceExcelImportMode? Show(Window? owner, AcceptanceExcelImportTarget target)
    {
        var dialog = new ImportModeDialog(target)
        {
            Owner = owner
        };

        dialog.ShowDialog();
        return dialog._selectedMode;
    }

    private void AppendButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedMode = AcceptanceExcelImportMode.Append;
        DialogResult = true;
    }

    private void ReplaceButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedMode = AcceptanceExcelImportMode.Replace;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedMode = null;
        DialogResult = false;
    }
}
