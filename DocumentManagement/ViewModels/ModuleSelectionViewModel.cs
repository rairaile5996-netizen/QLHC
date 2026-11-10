using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentManagement.Models;
using DocumentManagement.Services;
using DocumentManagement.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.ViewModels;

public partial class ModuleSelectionViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthService _authService;
    private readonly IGuaranteeService _guaranteeService;


    public ModuleSelectionViewModel(
        IServiceProvider serviceProvider,
        IAuthService authService,
        IGuaranteeService guaranteeService,
        IAppUpdateService appUpdateService)
    {
        _serviceProvider = serviceProvider;
        _authService = authService;
        _guaranteeService = guaranteeService;

        _ = RefreshGuaranteeAlertDataAsync();

        // Updater mới tự quản lý toàn bộ UI thông qua UpdateHostView.
        // ViewModel chỉ cần kích hoạt kiểm tra cập nhật khi khởi động.
        _ = appUpdateService.CheckForUpdatesOnStartupAsync();
    }


    // =========================================================
    // CURRENT USER
    // =========================================================

    public User? CurrentUser =>
        _authService.CurrentUser;


    public string UserFullName =>
        CurrentUser?.FullName ??
        "Người dùng";


    public string UserInitial =>
        CurrentUser != null &&
        !string.IsNullOrEmpty(CurrentUser.FullName)
            ? CurrentUser.FullName[0]
                .ToString()
                .ToUpper()
            : "?";


    // =========================================================
    // GUARANTEE NOTIFICATIONS
    // =========================================================

    [ObservableProperty]
    private ObservableCollection<GuaranteeAlertItem>
        _guaranteeAlerts = [];


    [ObservableProperty]
    private bool
        _isGuaranteeNotificationOpen;


    [ObservableProperty]
    private int
        _guaranteeAlertCount;


    public bool HasGuaranteeAlerts =>
        GuaranteeAlertCount > 0;


    // =========================================================
    // REFRESH GUARANTEE ALERTS
    // =========================================================

    public async Task RefreshGuaranteeAlertDataAsync()
    {
        var alerts =
            await _guaranteeService
                .GetActiveAlertsAsync();


        GuaranteeAlerts =
            new ObservableCollection<GuaranteeAlertItem>(
                alerts);


        GuaranteeAlertCount =
            GuaranteeAlerts.Count;


        OnPropertyChanged(
            nameof(HasGuaranteeAlerts));
    }


    // =========================================================
    // TOGGLE GUARANTEE PANEL
    // =========================================================

    [RelayCommand]
    private void ToggleGuaranteeNotifications()
    {
        IsGuaranteeNotificationOpen =
            !IsGuaranteeNotificationOpen;
    }


    // =========================================================
    // CLOSE GUARANTEE PANEL
    // =========================================================

    [RelayCommand]
    private void CloseGuaranteeNotificationPanel()
    {
        IsGuaranteeNotificationOpen =
            false;
    }


    // =========================================================
    // OPEN GUARANTEE ALERT
    // =========================================================

    [RelayCommand]
    private async Task OpenGuaranteeAlert(
        GuaranteeAlertItem? alert)
    {
        if (alert == null)
        {
            return;
        }


        var shellWindow =
            _serviceProvider
                .GetRequiredService<ShellWindow>();


        var shellViewModel =
            shellWindow.DataContext
            as ShellViewModel;


        if (shellViewModel == null)
        {
            MessageBox.Show(
                "Không thể khởi tạo màn hình quản lý hợp đồng.",
                "Lỗi",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }


        var didNavigate =
            await shellViewModel
                .InitializeAndNavigateToGuaranteeAsync(
                    alert.DocumentId,
                    alert.GuaranteeId);


        if (!didNavigate)
        {
            await RefreshGuaranteeAlertDataAsync();


            MessageBox.Show(
                "Không còn tìm thấy hồ sơ hoặc bảo lãnh tương ứng. Danh sách cảnh báo đã được làm mới.",
                "Thông báo",
                MessageBoxButton.OK,
                MessageBoxImage.Information);


            return;
        }


        Application.Current.MainWindow =
            shellWindow;


        shellWindow.Show();


        CloseCurrentWindow();
    }


    // =========================================================
    // CONTRACT MANAGEMENT
    // =========================================================

    [RelayCommand]
    private async Task SelectContractManagement()
    {
        var shellWindow =
            _serviceProvider
                .GetRequiredService<ShellWindow>();


        var shellViewModel =
            shellWindow.DataContext
            as ShellViewModel;


        await shellViewModel!
            .InitializeAndNavigateToDashboardAsync();


        Application.Current.MainWindow =
            shellWindow;


        shellWindow.Show();


        IsGuaranteeNotificationOpen =
            false;


        CloseCurrentWindow();
    }


    // =========================================================
    // ACCEPTANCE DOCUMENTS
    // =========================================================

    [RelayCommand]
    private async Task SelectAcceptanceDocuments()
    {
        var mainViewModel =
            _serviceProvider
                .GetRequiredService<MainViewModel>();


        await mainViewModel
            .InitializeAsync();


        var cvxdWindow =
            _serviceProvider
                .GetRequiredService<CvxdWindow>();


        if (cvxdWindow.DataContext
            is CvxdWindowViewModel cvxdViewModel)
        {
            cvxdViewModel.Initialize();
        }


        Application.Current.MainWindow =
            cvxdWindow;


        cvxdWindow.Show();


        CloseCurrentWindow();
    }

    [RelayCommand]
    private void SelectConstructionDirectives()
    {
        var previous = Application.Current.MainWindow;
        ConstructionDirectivesWindow? window = null;
        try
        {
            window = _serviceProvider.GetRequiredService<ConstructionDirectivesWindow>();
            window.Show();
            window.UpdateLayout();
            Application.Current.MainWindow = window;
            CloseCurrentWindow();
        }
        catch (Exception ex)
        {
            Application.Current.MainWindow = previous;
            window?.Close();
            var log = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement", "directive-open-error.log");
            try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(log)!); System.IO.File.AppendAllText(log, DateTimeOffset.Now + Environment.NewLine + ex + Environment.NewLine); }
            catch { /* Keep the existing window usable even if diagnostics cannot be written. */ }
            MessageBox.Show(previous, "Không thể mở Quản lý chỉ thị. Bạn có thể tiếp tục dùng app.\nChi tiết lỗi: " + ex.Message + "\nFile chẩn đoán: " + log, "Mở Quản lý chỉ thị", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    // =========================================================
    // EXIT APP
    // =========================================================

    [RelayCommand]
    private static void ExitApp()
    {
        if (ConfirmDialog.Show(
                "Xác nhận",
                "Bạn có muốn thoát ứng dụng không?",
                null,
                "Thoát",
                "Hủy"))
        {
            Application.Current.Shutdown();
        }
    }


    // =========================================================
    // LOCK APP
    // =========================================================

    [RelayCommand]
    private void LockApp()
    {
        _authService.Lock();


        var loginWindow =
            _serviceProvider
                .GetRequiredService<LoginWindow>();


        Application.Current.MainWindow =
            loginWindow;


        loginWindow.WindowState =
            WindowState.Normal;


        loginWindow.ShowInTaskbar =
            true;


        loginWindow.Show();


        loginWindow.Activate();


        CloseCurrentWindow();
    }


    // =========================================================
    // CLOSE MODULE SELECTION WINDOW
    // =========================================================

    private static void CloseCurrentWindow()
    {
        Application.Current.Windows
            .OfType<ModuleSelectionWindow>()
            .FirstOrDefault()?
            .Close();
    }
}
