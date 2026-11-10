using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DocumentManagement.Utilities;
using DocumentManagement.Views.Updates;

namespace DocumentManagement.Services;

public class AppUpdateService : IAppUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };


    // =========================================================
    // UPDATE SERVICE
    // =========================================================

    private readonly HttpClient _httpClient;

    private int _hasChecked;

    private PendingUpdate? _pendingUpdate;


    // =========================================================
    // UPDATE UI STATE
    // =========================================================

    private enum UpdateUiState
    {
        Hidden,
        Prompt,
        Downloading,
        Ready,
        Error
    }


    private UpdateUiState _uiState =
        UpdateUiState.Hidden;


    private UpdateUiState _renderedState =
        UpdateUiState.Hidden;


    private UpdateHostView? _renderedHost;


    // =========================================================
    // COMPACT STATE
    //
    // true:
    // updater đang được user thu gọn.
    //
    // false:
    // updater đang mở đầy đủ.
    //
    // State này thuộc về toàn ứng dụng,
    // không thuộc riêng ModuleSelection / Shell / Cvxd.
    // =========================================================

    private bool _isCompact;


    // =========================================================
    // PROMPT STATE
    // =========================================================

    private TaskCompletionSource<bool>? _promptDecision;

    private string? _promptVersion;

    private string? _promptCurrentVersion;

    private string? _promptReleaseNotes;

    private IReadOnlyList<UpdateChange> _promptChanges =
        Array.Empty<UpdateChange>();

    private long? _promptPackageSizeBytes;


    // =========================================================
    // DOWNLOAD / READY / ERROR STATE
    // =========================================================

    private string? _currentVersion;

    private AppUpdateProgress? _lastProgress;

    private string? _errorDetails;


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public AppUpdateService(
        HttpClient httpClient)
    {
        _httpClient =
            httpClient;


        _httpClient.Timeout =
            TimeSpan.FromSeconds(30);

    }


    // =========================================================
    // ACTIVE UPDATE HOST
    //
    // Mỗi Window có UpdateHostView sẽ gọi SetActiveHost()
    // khi Window được Activated.
    //
    // Không còn polling / DispatcherTimer.
    // =========================================================

    public void SetActiveHost(
        UpdateHostView host)
    {
        if (Application.Current == null)
        {
            return;
        }


        void ApplyHost()
        {
            // Vẫn là host hiện tại thì không cần dựng lại UI.
            if (ReferenceEquals(
                    _renderedHost,
                    host))
            {
                host.SetUpdateService(
                    this);

                return;
            }


            // Chuyển toàn bộ state hiện tại sang host mới ngay lập tức.
            RenderCurrentState(
                host,
                true);
        }


        if (Application.Current.Dispatcher.CheckAccess())
        {
            ApplyHost();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(
                ApplyHost);
        }
    }


    // =========================================================
    // HOST BÁO USER ĐÃ THU GỌN / MỞ LẠI
    // =========================================================

    private void Host_CompactStateChanged(
        bool isCompact)
    {
        _isCompact =
            isCompact;
    }


    // =========================================================
    // RENDER CURRENT UPDATE STATE
    // =========================================================

    private void RenderCurrentState(
        UpdateHostView? host = null,
        bool force = false)
    {
        host ??=
            _renderedHost;


        if (host == null)
        {
            return;
        }


        if (!force &&
            ReferenceEquals(
                host,
                _renderedHost) &&
            _renderedState ==
            _uiState)
        {
            return;
        }


        // =====================================================
        // Nếu chuyển Window:
        // bỏ event + ẩn host cũ
        // =====================================================

        if (_renderedHost != null &&
            !ReferenceEquals(
                _renderedHost,
                host))
        {
            _renderedHost.CompactStateChanged -=
                Host_CompactStateChanged;


            _renderedHost.HideHost();
        }


        // =====================================================
        // Gắn service cho host mới
        // =====================================================

        host.SetUpdateService(
            this);


        // Tránh đăng ký event trùng nhiều lần.
        host.CompactStateChanged -=
            Host_CompactStateChanged;


        host.CompactStateChanged +=
            Host_CompactStateChanged;


        // =====================================================
        // RENDER STATE
        // =====================================================

        switch (_uiState)
        {
            case UpdateUiState.Hidden:
                {
                    host.HideHost();

                    break;
                }


            case UpdateUiState.Prompt:
                {
                    RenderPrompt(
                        host);

                    break;
                }


            case UpdateUiState.Downloading:
                {
                    if (_lastProgress != null)
                    {
                        host.ShowDownloading(
                            _lastProgress,
                            _currentVersion,
                            _isCompact);
                    }

                    break;
                }


            case UpdateUiState.Ready:
                {
                    host.ShowReady(
                        _currentVersion,
                        _isCompact);

                    break;
                }


            case UpdateUiState.Error:
                {
                    host.ShowError(
                        _errorDetails);

                    break;
                }
        }


        _renderedHost =
            host;


        _renderedState =
            _uiState;
    }


    // =========================================================
    // RENDER PROMPT
    // =========================================================

    private void RenderPrompt(
        UpdateHostView host)
    {
        if (_promptDecision == null ||
            _promptDecision.Task.IsCompleted ||
            string.IsNullOrWhiteSpace(
                _promptVersion))
        {
            return;
        }


        host.ShowUpdatePrompt(
            _promptVersion,
            _promptCurrentVersion ??
            AppVersionInfo.CurrentVersion,
            _promptReleaseNotes ??
            string.Empty,
            _promptChanges,
            _promptPackageSizeBytes,
            decision =>
            {
                _promptDecision?
                    .TrySetResult(
                        decision);
            });
    }


    // =========================================================
    // SHOW UPDATE PROMPT
    // =========================================================

    private async Task<bool> ShowUpdatePromptAsync(
        string version,
        string currentVersion,
        string releaseNotes,
        IReadOnlyList<UpdateChange> changes,
        long? packageSizeBytes)
    {
        var completion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);


        await Application.Current.Dispatcher
            .InvokeAsync(() =>
            {
                _promptDecision =
                    completion;


                _promptVersion =
                    version;


                _promptCurrentVersion =
                    currentVersion;


                _promptReleaseNotes =
                    releaseNotes;


                _promptChanges =
                    changes ??
                    Array.Empty<UpdateChange>();


                _promptPackageSizeBytes =
                    packageSizeBytes;


                _currentVersion =
                    version;


                // Prompt luôn mở đầy đủ.
                _isCompact =
                    false;


                _uiState =
                    UpdateUiState.Prompt;


                RenderCurrentState(
                    force: true);
            });


        var result =
            await completion.Task;


        await Application.Current.Dispatcher
            .InvokeAsync(() =>
            {
                // User chọn "Để sau"
                if (!result)
                {
                    _uiState =
                        UpdateUiState.Hidden;


                    _isCompact =
                        false;


                    _renderedHost?
                        .HideHost();


                    _renderedState =
                        UpdateUiState.Hidden;
                }
            });


        return result;
    }


    // =========================================================
    // CHECK UPDATE
    // =========================================================

    public async Task CheckForUpdatesOnStartupAsync(
        IProgress<AppUpdateProgress>? progress = null)
    {
        if (Interlocked.Exchange(
                ref _hasChecked,
                1) == 1)
        {
            return;
        }


        try
        {
            await Task.Delay(
                1200);


            // =================================================
            // KIỂM TRA UPDATE ĐÃ DOWNLOAD TỪ LẦN TRƯỚC
            // =================================================

            if (await TryRestorePendingUpdateAsync(
                    progress))
            {
                return;
            }


            // =================================================
            // LOAD SETTINGS
            // =================================================

            var settings =
                await LoadSettingsAsync();


            if (!settings.CheckOnStartup ||
                !UpdateVersionComparer
                    .IsConfiguredValue(
                        settings.ManifestUrl))
            {
                return;
            }


            // =================================================
            // LOAD MANIFEST
            // =================================================

            var manifest =
                await LoadManifestAsync(
                    settings.ManifestUrl);


            if (manifest == null ||
                !UpdateVersionComparer
                    .IsConfiguredValue(
                        manifest.PackageUrl))
            {
                return;
            }


            // =================================================
            // VERSION CHECK
            // =================================================

            var currentVersion =
                AppVersionInfo.CurrentVersion;


            if (!UpdateVersionComparer
                    .IsNewer(
                        currentVersion,
                        manifest.Version))
            {
                return;
            }


            var changeSummary =
                manifest.ReleaseNotes;


            // =================================================
            // GITHUB ASSET
            // =================================================

            var releaseAsset =
                await TryResolveGitHubAssetAsync(
                    manifest.PackageUrl);


            if (releaseAsset is not null)
            {
                manifest.PackageUrl =
                    releaseAsset.Value.Url;
            }


            var packageSizeBytes =
                releaseAsset?.Size ??
                await TryGetPackageSizeAsync(
                    manifest.PackageUrl);


            // =================================================
            // PROMPT
            // =================================================

            var shouldUpdate =
                await ShowUpdatePromptAsync(
                    manifest.Version,
                    currentVersion,
                    changeSummary,
                    manifest.Changes,
                    packageSizeBytes);


            if (!shouldUpdate)
            {
                return;
            }


            // =================================================
            // DOWNLOAD START
            // =================================================

            var initialProgress =
                new AppUpdateProgress(
                    "Đang tải bản cập nhật...",
                    0);


            progress?.Report(
                initialProgress);


            ShowDownloadingHost(
                initialProgress,
                manifest.Version);


            // =================================================
            // SIMULATION
            // =================================================

            if (settings.SimulateDownload)
            {
                var simulatedProgress =
                    new Progress<AppUpdateProgress>(
                        updateProgress =>
                        {
                            progress?.Report(
                                updateProgress);


                            UpdateDownloadingHost(
                                updateProgress);
                        });


                await SimulateDownloadAsync(
                    manifest.Version,
                    simulatedProgress,
                    packageSizeBytes);


                return;
            }


            // =================================================
            // REAL DOWNLOAD
            // =================================================

            await DownloadAndApplyUpdateAsync(
                manifest,
                progress);
        }
        catch (Exception ex)
        {
            ShowErrorHost(
                string.IsNullOrWhiteSpace(
                    ex.Message)
                    ? "Kiểm tra kết nối mạng rồi thử lại."
                    : ex.Message);


            progress?.Report(
                new AppUpdateProgress(
                    "Không thể tải bản cập nhật.",
                    null,
                    false));


            // Không làm hỏng luồng khởi động
            // nếu update check lỗi.
        }
    }


    // =========================================================
    // RETRY
    // =========================================================

    public async Task RetryAsync()
    {
        Interlocked.Exchange(
            ref _hasChecked,
            0);


        _isCompact =
            false;


        await CheckForUpdatesOnStartupAsync();
    }


    // =========================================================
    // SHOW DOWNLOADING
    //
    // Hàm này chỉ dùng khi bắt đầu download.
    //
    // Vì vậy ở đây reset compact = false.
    // =========================================================

    private void ShowDownloadingHost(
        AppUpdateProgress progress,
        string? version = null)
    {
        if (Application.Current == null)
        {
            return;
        }


        Application.Current.Dispatcher
            .BeginInvoke(
                new Action(() =>
                {
                    _currentVersion =
                        version ??
                        _currentVersion;


                    _lastProgress =
                        progress;


                    // Download vừa bắt đầu:
                    // hiển thị card đầy đủ.
                    _isCompact =
                        false;


                    _uiState =
                        progress.CanInstall
                            ? UpdateUiState.Ready
                            : UpdateUiState.Downloading;


                    RenderCurrentState(
                        force: true);
                }));
    }


    // =========================================================
    // UPDATE DOWNLOAD PROGRESS
    // =========================================================

    private void UpdateDownloadingHost(
        AppUpdateProgress progress)
    {
        if (Application.Current == null)
        {
            return;
        }


        Application.Current.Dispatcher
            .Invoke(() =>
            {
                _lastProgress =
                    progress;


                // =================================================
                // DOWNLOAD XONG
                // =================================================

                if (progress.CanInstall)
                {
                    _uiState =
                        UpdateUiState.Ready;


                    // Không reset _isCompact ở đây.
                    //
                    // Nếu user đang thu gọn lúc download,
                    // Ready cũng tiếp tục thu gọn.
                    RenderCurrentState(
                        force: true);


                    return;
                }


                _uiState =
                    UpdateUiState.Downloading;


                var host =
                    _renderedHost;


                if (host == null)
                {
                    // Chưa có Window nào đăng ký host.
                    // State/progress vẫn được giữ lại và sẽ render
                    // ngay khi Window tiếp theo gọi SetActiveHost().
                    return;
                }


                // =================================================
                // VẪN ĐANG Ở HOST HIỆN TẠI
                //
                // Chỉ update dữ liệu progress,
                // không dựng lại UserControl.
                // =================================================

                if (_renderedState ==
                    UpdateUiState.Downloading)
                {
                    host.UpdateProgress(
                        progress);


                    return;
                }


                // Nếu state UI chưa khớp thì dựng lại.
                RenderCurrentState(
                    host,
                    true);
            });
    }


    // =========================================================
    // READY
    // =========================================================

    private void ShowUpdateReadyHost(
        string? version = null)
    {
        if (Application.Current == null)
        {
            return;
        }


        Application.Current.Dispatcher
            .BeginInvoke(
                new Action(() =>
                {
                    _currentVersion =
                        version ??
                        _currentVersion;


                    _uiState =
                        UpdateUiState.Ready;


                    // Không reset compact.
                    //
                    // Nếu download đang thu gọn,
                    // Ready cũng thu gọn.
                    RenderCurrentState(
                        force: true);
                }));
    }


    // =========================================================
    // ERROR
    // =========================================================

    private void ShowErrorHost(
        string? details = null)
    {
        if (Application.Current == null)
        {
            return;
        }


        Application.Current.Dispatcher
            .BeginInvoke(
                new Action(() =>
                {
                    _errorDetails =
                        details ??
                        "Kiểm tra kết nối mạng rồi thử lại.";


                    // Error nên mở đầy đủ
                    // để user thấy lỗi và nút Retry.
                    _isCompact =
                        false;


                    _uiState =
                        UpdateUiState.Error;


                    RenderCurrentState(
                        force: true);
                }));
    }


    // =========================================================
    // SIMULATE DOWNLOAD
    // =========================================================

    private async Task SimulateDownloadAsync(
        string version,
        IProgress<AppUpdateProgress>? progress,
        long? totalBytes = null)
    {
        var simulatedTotalBytes =
            totalBytes is > 0
                ? totalBytes.Value
                : 48_200_000L;


        for (var percent = 0;
             percent <= 100;
             percent += 5)
        {
            var downloadedBytes =
                simulatedTotalBytes *
                percent /
                100;


            progress?.Report(
                new AppUpdateProgress(
                    "Đang tải bản cập nhật...",
                    percent,
                    true,
                    false,
                    downloadedBytes,
                    simulatedTotalBytes,
                    simulatedTotalBytes /
                    30d));


            await Task.Delay(
                1500);
        }


        progress?.Report(
            new AppUpdateProgress(
                "Bản cập nhật đã tải xong.",
                100,
                true,
                true,
                simulatedTotalBytes,
                simulatedTotalBytes,
                simulatedTotalBytes /
                30d));
    }


    // =========================================================
    // SETTINGS
    // =========================================================

    private async Task<AppUpdateSettings>
        LoadSettingsAsync()
    {
        foreach (var candidatePath
                 in GetSettingsPaths())
        {
            if (!File.Exists(
                    candidatePath))
            {
                continue;
            }


            await using var stream =
                File.OpenRead(
                    candidatePath);


            var settings =
                await JsonSerializer
                    .DeserializeAsync<AppUpdateSettings>(
                        stream,
                        JsonOptions);


            if (settings != null)
            {
                return settings;
            }
        }


        return new AppUpdateSettings();
    }


    private static IEnumerable<string>
        GetSettingsPaths()
    {
        var appDataPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .ApplicationData),
                "DocumentManagement",
                "update-settings.json");


        var appFolderPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "Resources",
                "update-settings.json");


        yield return appDataPath;

        yield return appFolderPath;
    }


    // =========================================================
    // LOAD MANIFEST
    // =========================================================

    private async Task<AppUpdateManifest?>
        LoadManifestAsync(
            string manifestLocation)
    {
        if (TryGetLocalPath(
                manifestLocation,
                out var localPath))
        {
            if (!File.Exists(
                    localPath))
            {
                return null;
            }


            await using var stream =
                File.OpenRead(
                    localPath);


            return await JsonSerializer
                .DeserializeAsync<AppUpdateManifest>(
                    stream,
                    JsonOptions);
        }


        await using var streamFromWeb =
            await _httpClient
                .GetStreamAsync(
                    manifestLocation);


        return await JsonSerializer
            .DeserializeAsync<AppUpdateManifest>(
                streamFromWeb,
                JsonOptions);
    }


    // =========================================================
    // PACKAGE SIZE
    // =========================================================

    private async Task<long?>
        TryGetPackageSizeAsync(
            string packageLocation)
    {
        if (TryGetLocalPath(
                packageLocation,
                out var localPath))
        {
            return new FileInfo(
                    localPath)
                .Length;
        }


        var githubSize =
            await TryGetGitHubReleaseSizeAsync(
                packageLocation);


        if (githubSize.HasValue)
        {
            return githubSize;
        }


        try
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Head,
                    packageLocation);


            using var response =
                await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption
                        .ResponseHeadersRead);


            return response.IsSuccessStatusCode
                ? response.Content
                    .Headers
                    .ContentLength
                : null;
        }
        catch
        {
            return null;
        }
    }


    // =========================================================
    // RESOLVE GITHUB ASSET
    // =========================================================

    private async Task<(string Url, long Size)?>
        TryResolveGitHubAssetAsync(
            string packageLocation)
    {
        if (!Uri.TryCreate(
                packageLocation,
                UriKind.Absolute,
                out var packageUri) ||
            !packageUri.Host.Equals(
                "github.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }


        var segments =
            packageUri.AbsolutePath
                .Trim('/')
                .Split(
                    '/',
                    StringSplitOptions
                        .RemoveEmptyEntries);


        if (segments.Length < 6 ||
            !segments[2].Equals(
                "releases",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }


        try
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    $"https://api.github.com/repos/{segments[0]}/{segments[1]}/releases/latest");


            request.Headers.UserAgent
                .ParseAdd(
                    "DocumentManagement-AppUpdate");


            using var response =
                await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption
                        .ResponseHeadersRead);


            if (!response.IsSuccessStatusCode)
            {
                return null;
            }


            await using var stream =
                await response.Content
                    .ReadAsStreamAsync();


            using var document =
                await JsonDocument
                    .ParseAsync(
                        stream);


            if (!document.RootElement
                    .TryGetProperty(
                        "assets",
                        out var assets))
            {
                return null;
            }


            var assetName =
                Uri.UnescapeDataString(
                    segments[^1]);


            foreach (var asset
                     in assets.EnumerateArray())
            {
                if (asset.TryGetProperty(
                        "name",
                        out var name) &&

                    string.Equals(
                        name.GetString(),
                        assetName,
                        StringComparison.OrdinalIgnoreCase) &&

                    asset.TryGetProperty(
                        "browser_download_url",
                        out var url) &&

                    asset.TryGetProperty(
                        "size",
                        out var size) &&

                    size.TryGetInt64(
                        out var bytes))
                {
                    return (
                        url.GetString() ??
                        packageLocation,
                        bytes);
                }
            }
        }
        catch
        {
        }


        return null;
    }


    // =========================================================
    // GITHUB RELEASE SIZE
    // =========================================================

    private async Task<long?>
        TryGetGitHubReleaseSizeAsync(
            string packageLocation)
    {
        if (!Uri.TryCreate(
                packageLocation,
                UriKind.Absolute,
                out var packageUri) ||

            !packageUri.Host.Equals(
                "github.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }


        var segments =
            packageUri.AbsolutePath
                .Trim('/')
                .Split(
                    '/',
                    StringSplitOptions
                        .RemoveEmptyEntries);


        if (segments.Length < 6 ||
            !segments[2].Equals(
                "releases",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }


        var owner =
            segments[0];


        var repository =
            segments[1];


        var assetName =
            Uri.UnescapeDataString(
                segments[^1]);


        try
        {
            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    $"https://api.github.com/repos/{owner}/{repository}/releases/latest");


            request.Headers.UserAgent
                .ParseAdd(
                    "DocumentManagement-AppUpdate");


            using var response =
                await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption
                        .ResponseHeadersRead);


            if (!response.IsSuccessStatusCode)
            {
                return null;
            }


            await using var stream =
                await response.Content
                    .ReadAsStreamAsync();


            using var document =
                await JsonDocument
                    .ParseAsync(
                        stream);


            if (!document.RootElement
                    .TryGetProperty(
                        "assets",
                        out var assets))
            {
                return null;
            }


            foreach (var asset
                     in assets.EnumerateArray())
            {
                if (asset.TryGetProperty(
                        "name",
                        out var name) &&

                    string.Equals(
                        name.GetString(),
                        assetName,
                        StringComparison.OrdinalIgnoreCase) &&

                    asset.TryGetProperty(
                        "size",
                        out var size) &&

                    size.TryGetInt64(
                        out var bytes))
                {
                    return bytes;
                }
            }
        }
        catch
        {
            // Fallback sang Content-Length
            // nếu GitHub API không dùng được.
        }


        return null;
    }


    // =========================================================
    // INSTALL PENDING UPDATE
    // =========================================================

    public async Task InstallPendingUpdateAsync()
    {
        if (_pendingUpdate == null)
        {
            return;
        }


        var pendingUpdate =
            _pendingUpdate;


        DeletePendingUpdateRecord();


        var scriptPath =
            Path.Combine(
                pendingUpdate.TempRoot,
                "apply-update.ps1");


        await File.WriteAllTextAsync(
            scriptPath,
            BuildPowerShellScript(
                pendingUpdate.StagedRoot,
                pendingUpdate.InstallDirectory,
                pendingUpdate.RestartExecutable,
                Process.GetCurrentProcess().Id));


        Process.Start(
            new ProcessStartInfo
            {
                FileName =
                    "powershell.exe",

                Arguments =
                    $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",

                UseShellExecute =
                    false,

                CreateNoWindow =
                    true,

                WorkingDirectory =
                    pendingUpdate.TempRoot
            });


        Application.Current.Shutdown();
    }


    // =========================================================
    // DOWNLOAD + EXTRACT UPDATE
    // =========================================================

    private async Task DownloadAndApplyUpdateAsync(
        AppUpdateManifest manifest,
        IProgress<AppUpdateProgress>? progress)
    {
        var currentExePath =
            Environment.ProcessPath ??
            Process.GetCurrentProcess()
                .MainModule?
                .FileName;


        if (string.IsNullOrWhiteSpace(
                currentExePath))
        {
            throw new InvalidOperationException(
                "Không xác định được file chạy hiện tại.");
        }


        var installDirectory =
            Path.GetDirectoryName(
                currentExePath);


        if (string.IsNullOrWhiteSpace(
                installDirectory))
        {
            throw new InvalidOperationException(
                "Không xác định được thư mục cài đặt hiện tại.");
        }


        var tempRoot =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData),
                "DocumentManagement",
                "Updates",
                Guid.NewGuid()
                    .ToString("N"));


        Directory.CreateDirectory(
            tempRoot);


        var packagePath =
            Path.Combine(
                tempRoot,
                "update-package.zip");


        var extractedPath =
            Path.Combine(
                tempRoot,
                "package");


        Directory.CreateDirectory(
            extractedPath);


        // =====================================================
        // DOWNLOAD
        // =====================================================

        var downloadProgress =
            new Progress<AppUpdateProgress>(
                updateProgress =>
                {
                    progress?.Report(
                        updateProgress);


                    UpdateDownloadingHost(
                        updateProgress);
                });


        await DownloadPackageAsync(
            manifest.PackageUrl,
            packagePath,
            downloadProgress);


        // =====================================================
        // EXTRACT
        // =====================================================

        progress?.Report(
            new AppUpdateProgress(
                "Đang chuẩn bị bản cập nhật...",
                null));


        UpdateDownloadingHost(
            new AppUpdateProgress(
                "Đang giải nén bản cập nhật...",
                null));


        await Task.Run(() =>
            ZipFile.ExtractToDirectory(
                packagePath,
                extractedPath,
                true));


        // =====================================================
        // FIND APP EXE
        // =====================================================

        var entryExecutable =
            string.IsNullOrWhiteSpace(
                manifest.EntryExecutable)
                ? Path.GetFileName(
                    currentExePath)
                : manifest.EntryExecutable;


        var stagedRoot =
            ResolvePackageRoot(
                extractedPath,
                entryExecutable!);


        if (stagedRoot == null)
        {
            throw new InvalidOperationException(
                $"Không tìm thấy {entryExecutable} trong gói cập nhật.");
        }


        // =====================================================
        // CREATE PENDING UPDATE
        // =====================================================

        var restartExecutable =
            Path.Combine(
                installDirectory,
                entryExecutable!);


        _pendingUpdate =
            new PendingUpdate(
                tempRoot,
                stagedRoot,
                installDirectory,
                restartExecutable);


        await SavePendingUpdateRecordAsync(
            _pendingUpdate);


        // =====================================================
        // READY
        // =====================================================

        ShowUpdateReadyHost(
            manifest.Version);


        progress?.Report(
            new AppUpdateProgress(
                "Đã tải xong bản mới. Vui lòng chọn thời điểm cài đặt.",
                100,
                true,
                true));
    }


    // =========================================================
    // RESTORE PENDING UPDATE
    // =========================================================

    private async Task<bool>
        TryRestorePendingUpdateAsync(
            IProgress<AppUpdateProgress>? progress)
    {
        var pendingPath =
            GetPendingUpdatePath();


        if (!File.Exists(
                pendingPath))
        {
            return false;
        }


        try
        {
            await using var stream =
                File.OpenRead(
                    pendingPath);


            var pendingUpdate =
                await JsonSerializer
                    .DeserializeAsync<PendingUpdate>(
                        stream,
                        JsonOptions);


            if (pendingUpdate == null ||
                !Directory.Exists(
                    pendingUpdate.StagedRoot) ||
                !File.Exists(
                    pendingUpdate.RestartExecutable))
            {
                DeletePendingUpdateRecord();

                return false;
            }


            _pendingUpdate =
                pendingUpdate;


            // Pending update từ lần chạy trước:
            // mặc định hiển thị Ready đầy đủ.
            _isCompact =
                false;


            ShowUpdateReadyHost();


            progress?.Report(
                new AppUpdateProgress(
                    "Bản cập nhật đã tải sẵn. Vui lòng chọn thời điểm cài đặt.",
                    100,
                    true,
                    true));


            return true;
        }
        catch
        {
            DeletePendingUpdateRecord();

            return false;
        }
    }


    // =========================================================
    // PENDING UPDATE PATH
    // =========================================================

    private static string
        GetPendingUpdatePath()
    {
        var updateDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData),
                "DocumentManagement",
                "Updates");


        Directory.CreateDirectory(
            updateDirectory);


        return Path.Combine(
            updateDirectory,
            "pending-update.json");
    }


    // =========================================================
    // SAVE PENDING UPDATE
    // =========================================================

    private static async Task
        SavePendingUpdateRecordAsync(
            PendingUpdate pendingUpdate)
    {
        await File.WriteAllTextAsync(
            GetPendingUpdatePath(),
            JsonSerializer.Serialize(
                pendingUpdate,
                JsonOptions));
    }


    // =========================================================
    // DELETE PENDING UPDATE
    // =========================================================

    private static void
        DeletePendingUpdateRecord()
    {
        var pendingPath =
            GetPendingUpdatePath();


        if (File.Exists(
                pendingPath))
        {
            File.Delete(
                pendingPath);
        }
    }


    // =========================================================
    // DOWNLOAD PACKAGE
    // =========================================================

    private async Task DownloadPackageAsync(
        string packageLocation,
        string destinationPath,
        IProgress<AppUpdateProgress>? progress)
    {
        // =====================================================
        // LOCAL FILE
        // =====================================================

        if (TryGetLocalPath(
                packageLocation,
                out var localPath))
        {
            await using var localSource =
                File.OpenRead(
                    localPath);


            await using var localDestination =
                File.Create(
                    destinationPath);


            var localTotalBytes =
                localSource.Length;


            var localBuffer =
                new byte[81920];


            long copiedBytes =
                0;


            var localStarted =
                Stopwatch.GetTimestamp();


            int localBytesRead;


            while ((localBytesRead =
                       await localSource.ReadAsync(
                           localBuffer.AsMemory(
                               0,
                               localBuffer.Length))) > 0)
            {
                await localDestination
                    .WriteAsync(
                        localBuffer.AsMemory(
                            0,
                            localBytesRead));


                copiedBytes +=
                    localBytesRead;


                progress?.Report(
                    new AppUpdateProgress(
                        "Đang tải bản cập nhật...",
                        copiedBytes *
                        100d /
                        localTotalBytes,
                        true,
                        false,
                        copiedBytes,
                        localTotalBytes,
                        copiedBytes /
                        Math.Max(
                            0.001,
                            Stopwatch
                                .GetElapsedTime(
                                    localStarted)
                                .TotalSeconds)));


                // Delay nhỏ cho demo local update.
                await Task.Delay(
                    50);
            }


            return;
        }


        // =====================================================
        // HTTP DOWNLOAD
        // =====================================================

        using var response =
            await _httpClient.GetAsync(
                packageLocation,
                HttpCompletionOption
                    .ResponseHeadersRead);


        response.EnsureSuccessStatusCode();


        var totalBytes =
            response.Content
                .Headers
                .ContentLength;


        await using var source =
            await response.Content
                .ReadAsStreamAsync();


        await using var destination =
            File.Create(
                destinationPath);


        var buffer =
            new byte[81920];


        long downloadedBytes =
            0;


        var downloadStarted =
            Stopwatch.GetTimestamp();


        int bytesRead;


        while ((bytesRead =
                   await source.ReadAsync(
                       buffer)) > 0)
        {
            await destination
                .WriteAsync(
                    buffer.AsMemory(
                        0,
                        bytesRead));


            downloadedBytes +=
                bytesRead;


            double? percent =
                totalBytes is > 0
                    ? downloadedBytes *
                      100d /
                      totalBytes.Value
                    : null;


            progress?.Report(
                new AppUpdateProgress(
                    "Đang tải bản cập nhật...",
                    percent,
                    true,
                    false,
                    downloadedBytes,
                    totalBytes,
                    downloadedBytes /
                    Math.Max(
                        0.001,
                        Stopwatch
                            .GetElapsedTime(
                                downloadStarted)
                            .TotalSeconds)));
        }
    }


    // =========================================================
    // RESOLVE PACKAGE ROOT
    // =========================================================

    private static string?
        ResolvePackageRoot(
            string extractedPath,
            string entryExecutable)
    {
        var matchingExecutable =
            Directory
                .EnumerateFiles(
                    extractedPath,
                    entryExecutable,
                    SearchOption.AllDirectories)
                .FirstOrDefault();


        return matchingExecutable == null
            ? null
            : Path.GetDirectoryName(
                matchingExecutable);
    }


    // =========================================================
    // LOCAL PATH
    // =========================================================

    private static bool TryGetLocalPath(
        string location,
        out string localPath)
    {
        localPath =
            string.Empty;


        if (string.IsNullOrWhiteSpace(
                location))
        {
            return false;
        }


        if (Uri.TryCreate(
                location,
                UriKind.Absolute,
                out var uri) &&
            uri.IsFile)
        {
            localPath =
                uri.LocalPath;


            return true;
        }


        if (File.Exists(
                location))
        {
            localPath =
                Path.GetFullPath(
                    location);


            return true;
        }


        return false;
    }


    // =========================================================
    // POWERSHELL UPDATE SCRIPT
    // =========================================================

    private static string BuildPowerShellScript(
        string sourceDir,
        string targetDir,
        string restartExePath,
        int processId)
    {
        var escapedSource =
            EscapePowerShellLiteral(
                sourceDir);


        var escapedTarget =
            EscapePowerShellLiteral(
                targetDir);


        var escapedRestartExe =
            EscapePowerShellLiteral(
                restartExePath);


        return
$@"$ErrorActionPreference = 'Stop'
$sourceDir = '{escapedSource}'
$targetDir = '{escapedTarget}'
$restartExe = '{escapedRestartExe}'
$processIdToWait = {processId}
$logPath = Join-Path $env:TEMP 'DocumentManagementUpdater.log'

function Write-UpdateLog([string]$message) {{
    Add-Content -LiteralPath $logPath -Value (""$([DateTime]::Now.ToString('o')) $message"")
}}

try {{
    for ($i = 0; $i -lt 120; $i++) {{
        if (-not (Get-Process -Id $processIdToWait -ErrorAction SilentlyContinue)) {{
            break
        }}

        Start-Sleep -Milliseconds 500
    }}

    Write-UpdateLog 'Applying update files.'

    Get-ChildItem -LiteralPath $sourceDir -Force | ForEach-Object {{
        $destination = Join-Path $targetDir $_.Name

        if ($_.PSIsContainer) {{
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
        }}
        else {{
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
        }}
    }}

    Write-UpdateLog 'Restarting application.'

    Start-Process -FilePath $restartExe -WorkingDirectory $targetDir -WindowStyle Normal
}}
catch {{
    Write-UpdateLog ""Update failed: $($_.Exception.Message)""

    Add-Type -AssemblyName PresentationFramework

    [System.Windows.MessageBox]::Show(
        ""Không thể tự động cập nhật ứng dụng. Chi tiết lỗi: $($_.Exception.Message)"",
        'Cập nhật thất bại',
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Error
    ) | Out-Null

    if (Test-Path -LiteralPath $restartExe) {{
        Start-Process -FilePath $restartExe -WorkingDirectory $targetDir -WindowStyle Normal
    }}
}}";
    }


    // =========================================================
    // ESCAPE POWERSHELL STRING
    // =========================================================

    private static string
        EscapePowerShellLiteral(
            string value)
    {
        return value.Replace(
            "'",
            "''");
    }


    // =========================================================
    // PENDING UPDATE MODEL
    // =========================================================

    private sealed record PendingUpdate(
        string TempRoot,
        string StagedRoot,
        string InstallDirectory,
        string RestartExecutable);
}