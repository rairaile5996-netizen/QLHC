using System.Diagnostics;
using System.IO;

namespace DocumentManagement.Services;

/// <summary>
/// Service xử lý lỗi thống nhất cho toàn app
/// </summary>
public class ErrorHandlingService
{
    private Action<string>? _showToast;

    public ErrorHandlingService()
    {
    }

    /// <summary>
    /// Set toast action sau khi MainWindow được khởi tạo
    /// </summary>
    public void SetToastAction(Action<string> showToast)
    {
        _showToast = showToast;
    }

    /// <summary>
    /// Xử lý lỗi và hiển thị thông báo thân thiện cho user
    /// </summary>
    public void HandleError(Exception ex, string context, bool showToUser = true)
    {
        // Log chi tiết cho developer
        Debug.WriteLine($"[{context}] {ex.GetType().Name}: {ex.Message}");
        Debug.WriteLine($"StackTrace: {ex.StackTrace}");

        // Hiển thị thông báo thân thiện cho user
        if (showToUser)
        {
            var userMessage = GetUserFriendlyMessage(ex, context);
            _showToast?.Invoke($"❌ {userMessage}");
        }
    }

    /// <summary>
    /// Chuyển đổi exception thành thông báo thân thiện
    /// </summary>
    private string GetUserFriendlyMessage(Exception ex, string context)
    {
        return ex switch
        {
            FileNotFoundException => "Không tìm thấy file cần thiết",
            DirectoryNotFoundException => "Không tìm thấy thư mục",
            UnauthorizedAccessException => "Không có quyền truy cập file/thư mục",
            IOException => "Lỗi đọc/ghi file. File có thể đang được sử dụng",
            ArgumentException => "Dữ liệu đầu vào không hợp lệ",
            InvalidOperationException => "Thao tác không hợp lệ trong trạng thái hiện tại",
            TimeoutException => "Thao tác mất quá nhiều thời gian",
            System.Net.Http.HttpRequestException => "Lỗi kết nối mạng",
            _ => GetContextSpecificMessage(context, ex.Message)
        };
    }

    /// <summary>
    /// Thông báo lỗi dựa trên context cụ thể
    /// </summary>
    private string GetContextSpecificMessage(string context, string originalMessage)
    {
        return context.ToLower() switch
        {
            var c when c.Contains("ai") || c.Contains("gemini") => "Lỗi AI: Kiểm tra API key hoặc kết nối mạng",
            var c when c.Contains("excel") => "Lỗi đọc file Excel: Kiểm tra định dạng file",
            var c when c.Contains("word") => "Lỗi đọc file Word: File có thể bị hỏng",
            var c when c.Contains("database") => "Lỗi cơ sở dữ liệu: Thử khởi động lại app",
            var c when c.Contains("file") => "Lỗi xử lý file: Kiểm tra quyền truy cập",
            _ => $"Có lỗi xảy ra: {originalMessage}"
        };
    }

    /// <summary>
    /// Hiển thị thông báo thành công với icon
    /// </summary>
    public void ShowSuccess(string message)
    {
        _showToast?.Invoke($"✅ {message}");
    }

    /// <summary>
    /// Hiển thị cảnh báo với icon
    /// </summary>
    public void ShowWarning(string message)
    {
        _showToast?.Invoke($"⚠️ {message}");
    }

    /// <summary>
    /// Hiển thị lỗi với icon
    /// </summary>
    public void ShowError(string message)
    {
        _showToast?.Invoke($"❌ {message}");
    }

    /// <summary>
    /// Hiển thị thông tin với icon
    /// </summary>
    public void ShowInfo(string message)
    {
        _showToast?.Invoke($"ℹ️ {message}");
    }
}
