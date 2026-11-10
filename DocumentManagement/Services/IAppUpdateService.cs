using DocumentManagement.Views.Updates;

namespace DocumentManagement.Services;

public interface IAppUpdateService
{
    Task CheckForUpdatesOnStartupAsync(
        IProgress<AppUpdateProgress>? progress = null);

    Task InstallPendingUpdateAsync();

    Task RetryAsync();

    // Window đang active sẽ gọi hàm này
    // để updater chuyển sang UpdateHostView của Window đó ngay lập tức.
    void SetActiveHost(
        UpdateHostView host);
}