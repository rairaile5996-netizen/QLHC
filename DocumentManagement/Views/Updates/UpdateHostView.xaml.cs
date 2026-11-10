using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DocumentManagement.Services;

namespace DocumentManagement.Views.Updates;

public partial class UpdateHostView : UserControl
{
    private IAppUpdateService? _updateService;

    private string? _version;


    // =========================================================
    // BÁO CHO SERVICE BIẾT USER ĐANG THU GỌN / MỞ RỘNG
    // =========================================================

    public event Action<bool>? CompactStateChanged;


    private enum CompactMode
    {
        None,
        Downloading,
        Ready
    }


    private CompactMode _compactMode =
        CompactMode.None;


    private double _expandedCardWidth =
        390;


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public UpdateHostView()
    {
        InitializeComponent();
    }


    // =========================================================
    // UPDATE SERVICE
    // =========================================================

    public void SetUpdateService(
        IAppUpdateService updateService)
    {
        _updateService =
            updateService;
    }


    // =========================================================
    // PLACEMENT
    // =========================================================

    // Thông báo "Có phiên bản mới"
    // sẽ xuất hiện ở chính giữa cửa sổ.
    private void UseCenteredPlacement()
    {
        HorizontalAlignment =
            HorizontalAlignment.Center;

        VerticalAlignment =
            VerticalAlignment.Center;

        Margin =
            new Thickness(0);
    }


    // Download / Ready / Error
    // sẽ nằm ở góc phải dưới.
    private void UseCornerPlacement()
    {
        HorizontalAlignment =
            HorizontalAlignment.Right;

        VerticalAlignment =
            VerticalAlignment.Bottom;

        Margin =
            new Thickness(
                0,
                0,
                24,
                24);
    }


    // =========================================================
    // PROMPT ASYNC
    // =========================================================

    public Task<bool> ShowUpdatePromptAsync(
        string version,
        string currentVersion,
        string releaseNotes,
        IReadOnlyList<UpdateChange> changes,
        long? packageSizeBytes = null)
    {
        var completion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);


        ShowUpdatePrompt(
            version,
            currentVersion,
            releaseNotes,
            changes,
            packageSizeBytes,
            result =>
            {
                completion.TrySetResult(
                    result);
            });


        return completion.Task;
    }


    // =========================================================
    // PROMPT
    // =========================================================

    public void ShowUpdatePrompt(
        string version,
        string currentVersion,
        string releaseNotes,
        IReadOnlyList<UpdateChange> changes,
        long? packageSizeBytes,
        Action<bool> decisionCallback)
    {
        _version =
            version;


        var view =
            new UpdatePromptView();


        view.Configure(
            version,
            currentVersion,
            packageSizeBytes,
            changes,
            releaseNotes);


        view.DownloadRequested += (_, _) =>
        {
            decisionCallback(
                true);


            CollapseHost();
        };


        view.LaterRequested += (_, _) =>
        {
            decisionCallback(
                false);


            CollapseHost();
        };


        StateHost.Content =
            view;


        // Prompt cần user đưa ra quyết định
        // nên hiển thị chính giữa màn hình.
        UseCenteredPlacement();


        ShowState(
            460);
    }


    // =========================================================
    // DOWNLOADING
    // =========================================================

    public void ShowDownloading(
        AppUpdateProgress progress,
        string? version = null,
        bool compact = false)
    {
        _version =
            version ?? _version;


        var view =
            new UpdateDownloadingView();


        view.Configure(
            _version ?? "mới",
            DocumentManagement.Utilities
                .AppVersionInfo
                .CurrentVersion,
            progress);


        view.HideRequested += (_, _) =>
        {
            HideProgress();
        };


        StateHost.Content =
            view;


        ConfigureDownloadingCompactIndicator();


        UpdateCompactIndicator(
            progress);


        // Khi bắt đầu tải,
        // card chuyển về góc phải dưới.
        UseCornerPlacement();


        ShowState(
            360);


        if (compact)
        {
            SetCompactMode(
                CompactMode.Downloading);
        }
    }


    // =========================================================
    // UPDATE PROGRESS
    // =========================================================

    public void UpdateProgress(
        AppUpdateProgress progress)
    {
        UpdateCompactIndicator(
            progress);


        if (progress.CanInstall)
        {
            var wasCompact =
                _compactMode ==
                CompactMode.Downloading;


            ShowReady(
                _version,
                wasCompact);


            return;
        }


        if (StateHost.Content
            is UpdateDownloadingView view)
        {
            view.Update(
                progress);
        }
    }


    // =========================================================
    // READY
    // =========================================================

    public void ShowReady(
        string? version = null,
        bool compact = false)
    {
        _version =
            version ?? _version;


        var view =
            new UpdateReadyView();


        view.Configure(
            _version ?? "mới");


        view.InstallRequested +=
            InstallRequested;


        view.LaterRequested += (_, _) =>
        {
            HideReady();
        };


        StateHost.Content =
            view;


        ConfigureReadyCompactIndicator();


        // Ready tiếp tục nằm góc phải dưới.
        UseCornerPlacement();


        ShowState(
            390);


        if (compact)
        {
            SetCompactMode(
                CompactMode.Ready);
        }
    }


    // =========================================================
    // ERROR
    // =========================================================

    public void ShowError(
        string? details = null)
    {
        var view =
            new UpdateErrorView();


        view.Configure(
            details ??
            "Kiểm tra kết nối mạng rồi thử lại.");


        view.RetryRequested +=
            RetryRequested;


        view.LaterRequested += (_, _) =>
        {
            CollapseHost();
        };


        StateHost.Content =
            view;


        // Lỗi cũng nằm góc phải dưới.
        UseCornerPlacement();


        ShowState(
            390);
    }


    // =========================================================
    // INSTALL
    // =========================================================

    private async void InstallRequested(
        object? sender,
        EventArgs e)
    {
        if (_updateService == null)
        {
            return;
        }


        if (sender is UpdateReadyView view)
        {
            view.SetInstalling(
                true);
        }


        await _updateService
            .InstallPendingUpdateAsync();
    }


    // =========================================================
    // RETRY
    // =========================================================

    private async void RetryRequested(
        object? sender,
        EventArgs e)
    {
        if (_updateService == null)
        {
            return;
        }


        if (sender is not UpdateErrorView view)
        {
            return;
        }


        view.SetRetrying(
            true);


        try
        {
            await _updateService
                .RetryAsync();
        }
        finally
        {
            if (ReferenceEquals(
                    StateHost.Content,
                    view))
            {
                view.SetRetrying(
                    false);
            }
        }
    }


    // =========================================================
    // SHOW FULL CARD
    // =========================================================

    private void ShowState(
        double width)
    {
        _compactMode =
            CompactMode.None;


        UpdateCard.Width =
            width;


        IndicatorButton.Visibility =
            Visibility.Collapsed;


        StateHost.Visibility =
            Visibility.Visible;


        Visibility =
            Visibility.Visible;
    }


    // =========================================================
    // HIDE HOST
    // =========================================================

    public void HideHost()
    {
        CollapseHost();
    }


    private void CollapseHost()
    {
        _compactMode =
            CompactMode.None;


        IndicatorButton.Visibility =
            Visibility.Collapsed;


        StateHost.Visibility =
            Visibility.Visible;


        Visibility =
            Visibility.Collapsed;
    }


    // =========================================================
    // USER ẨN DOWNLOAD
    // =========================================================

    private void HideProgress()
    {
        if (_compactMode !=
            CompactMode.None)
        {
            return;
        }


        ConfigureDownloadingCompactIndicator();


        UseCornerPlacement();


        SetCompactMode(
            CompactMode.Downloading);


        CompactStateChanged?.Invoke(
            true);
    }


    // =========================================================
    // USER BẤM "ĐỂ SAU" KHI READY
    // =========================================================

    private void HideReady()
    {
        if (_compactMode !=
            CompactMode.None)
        {
            return;
        }


        ConfigureReadyCompactIndicator();


        UseCornerPlacement();


        SetCompactMode(
            CompactMode.Ready);


        CompactStateChanged?.Invoke(
            true);
    }


    // =========================================================
    // SET COMPACT MODE
    // =========================================================

    private void SetCompactMode(
        CompactMode mode)
    {
        _compactMode =
            mode;


        if (mode ==
            CompactMode.Ready)
        {
            ConfigureReadyCompactIndicator();
        }
        else if (mode ==
                 CompactMode.Downloading)
        {
            ConfigureDownloadingCompactIndicator();
        }


        UseCornerPlacement();


        ShowCompactIndicator();
    }


    // =========================================================
    // SHOW COMPACT
    // =========================================================

    private void ShowCompactIndicator()
    {
        if (!double.IsNaN(
                UpdateCard.Width) &&
            UpdateCard.Width > 0 &&
            UpdateCard.Width != 316)
        {
            _expandedCardWidth =
                UpdateCard.Width;
        }


        UpdateCard.Width =
            316;


        StateHost.Visibility =
            Visibility.Collapsed;


        IndicatorButton.Visibility =
            Visibility.Visible;


        Visibility =
            Visibility.Visible;
    }


    // =========================================================
    // CLICK COMPACT -> OPEN FULL CARD
    // =========================================================

    private void IndicatorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_compactMode ==
            CompactMode.None)
        {
            return;
        }


        _compactMode =
            CompactMode.None;


        UseCornerPlacement();


        UpdateCard.Width =
            _expandedCardWidth;


        IndicatorButton.Visibility =
            Visibility.Collapsed;


        StateHost.Visibility =
            Visibility.Visible;


        CompactStateChanged?.Invoke(
            false);
    }


    // =========================================================
    // COMPACT UI - DOWNLOADING
    // =========================================================

    private void ConfigureDownloadingCompactIndicator()
    {
        IndicatorDownloadIconBorder.Visibility =
            Visibility.Visible;


        IndicatorReadyIconBorder.Visibility =
            Visibility.Collapsed;


        IndicatorTitleText.Text =
            "Đang tải bản cập nhật";


        IndicatorPercentText.Visibility =
            Visibility.Visible;


        IndicatorProgressBar.Visibility =
            Visibility.Visible;


        if (string.IsNullOrWhiteSpace(
                IndicatorDetailsText.Text))
        {
            IndicatorDetailsText.Text =
                "Đang chuẩn bị tải...";
        }
    }


    // =========================================================
    // COMPACT UI - READY
    // =========================================================

    private void ConfigureReadyCompactIndicator()
    {
        IndicatorDownloadIconBorder.Visibility =
            Visibility.Collapsed;


        IndicatorReadyIconBorder.Visibility =
            Visibility.Visible;


        IndicatorTitleText.Text =
            "Bản cập nhật đã sẵn sàng";


        IndicatorDetailsText.Text =
            string.IsNullOrWhiteSpace(
                _version)
                ? "Nhấn để mở và cài đặt"
                : $"Phiên bản {_version} · Nhấn để cài đặt";


        IndicatorPercentText.Visibility =
            Visibility.Collapsed;


        IndicatorProgressBar.Visibility =
            Visibility.Collapsed;
    }


    // =========================================================
    // UPDATE COMPACT DOWNLOAD DATA
    // =========================================================

    private void UpdateCompactIndicator(
        AppUpdateProgress progress)
    {
        // Nếu đã Ready thì progress cũ
        // không được ghi đè UI Ready.
        if (_compactMode ==
            CompactMode.Ready)
        {
            return;
        }


        if (progress.Percent.HasValue)
        {
            var percent =
                Math.Clamp(
                    progress.Percent.Value,
                    0,
                    100);


            IndicatorProgressBar.IsIndeterminate =
                false;


            IndicatorProgressBar.Value =
                percent;


            IndicatorPercentText.Text =
                $"{percent:0}%";
        }
        else
        {
            IndicatorProgressBar.IsIndeterminate =
                true;


            IndicatorPercentText.Text =
                "...";
        }


        if (progress.TotalBytes is > 0 &&
            progress.DownloadedBytes.HasValue)
        {
            var downloadedMb =
                progress.DownloadedBytes.Value /
                1024d /
                1024d;


            var totalMb =
                progress.TotalBytes.Value /
                1024d /
                1024d;


            IndicatorDetailsText.Text =
                $"{downloadedMb:0.0} / {totalMb:0.0} MB";
        }
        else
        {
            IndicatorDetailsText.Text =
                string.IsNullOrWhiteSpace(
                    progress.Message)
                    ? "Đang tải..."
                    : progress.Message;
        }
    }
}