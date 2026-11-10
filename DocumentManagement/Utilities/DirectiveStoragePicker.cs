using System.Diagnostics;
using System.IO;
using System.Windows;
using DocumentManagement.Services;
using Microsoft.Win32;

namespace DocumentManagement.Utilities;

public static class DirectiveStoragePicker
{
    public static bool Choose(Window owner, ConstructionDirectiveService service)
    {
        var picker = new OpenFolderDialog { Title = "Chọn thư mục gốc lưu chỉ thị (app tự tạo thư mục năm)", Multiselect = false };
        if (Directory.Exists(service.StorageRoot)) picker.InitialDirectory = service.StorageRoot;
        if (picker.ShowDialog(owner) != true) return false;
        try
        {
            service.ChangeStorageRoot(picker.FolderName);
            DirectiveMessageDialog.Show(owner, "Thư mục chỉ thị", "Đã chọn thư mục gốc. File scan sẽ nằm trong các thư mục năm (ví dụ 2026). Dữ liệu cũ được sao chép an toàn và vẫn giữ ở vị trí trước đó.", false);
            return true;
        }
        catch (Exception ex) { DirectiveMessageDialog.Show(owner, "Không thể đổi thư mục", ex.Message, true); return false; }
    }
    public static void Open(Window owner, ConstructionDirectiveService service)
    {
        try { Directory.CreateDirectory(service.StorageRoot); Process.Start(new ProcessStartInfo(service.StorageRoot) { UseShellExecute = true }); }
        catch (Exception ex) { DirectiveMessageDialog.Show(owner, "Không thể mở thư mục", ex.Message, true); }
    }
}
