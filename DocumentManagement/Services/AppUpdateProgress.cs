namespace DocumentManagement.Services;

public sealed record AppUpdateProgress(
    string Message,
    double? Percent = null,
    bool IsActive = true,
    bool CanInstall = false,
    long? DownloadedBytes = null,
    long? TotalBytes = null,
    double? SpeedBytesPerSecond = null);
