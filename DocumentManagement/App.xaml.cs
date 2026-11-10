using System.IO;
using System.Threading;
using System.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.Http;
using DocumentManagement.Data;
using DocumentManagement.Services;
using DocumentManagement.Utilities;
using DocumentManagement.ViewModels;
using DocumentManagement.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement
{
    public partial class App : System.Windows.Application
    {
    private static bool _isDisplayingError = false;
    private static Mutex? _mutex;
    public static IServiceProvider ServiceProvider { get; private set; } = null!;
    public static Task DatabaseReadyTask { get; private set; } = Task.CompletedTask;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private static void WriteExceptionLog(string fileName, string prefix, Exception? ex)
    {
        try
        {
            System.IO.File.WriteAllText(
                fileName,
                $"{prefix}:\n\n{ex?.Message}\n\n{ex?.StackTrace}\n\nInner: {ex?.InnerException?.Message}");
        }
        catch
        {
            // Last-chance logging must never throw another exception.
        }
    }

    private static void TraceStartup(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "startup_trace.txt"),
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // Startup tracing is diagnostic only.
        }
    }


    private static void ShutdownSafely()
    {
        try
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Windows.Application.Current?.Shutdown();
            }));
        }
        catch
        {
            System.Windows.Application.Current?.Shutdown();
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        TraceStartup("OnStartup entered");
        const string appName = "DocumentManagement_SingleInstance_Mutex";
        _mutex = new Mutex(true, appName, out bool createdNew);
        TraceStartup($"Mutex createdNew={createdNew}");

        if (!createdNew)
        {
            // Tìm tiến trình đang chạy khác
            var currentProcess = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(currentProcess.ProcessName))
            {
                if (process.Id != currentProcess.Id)
                {
                    IntPtr hWnd = process.MainWindowHandle;
                    if (hWnd != IntPtr.Zero)
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                        SetForegroundWindow(hWnd);
                    }
                    break;
                }
            }

            System.Windows.Application.Current.Shutdown();
            return;
        }

        base.OnStartup(e);
        TraceStartup("base.OnStartup completed");

        // Apply the saved palette before any app window is shown so new installs
        // start consistently in Light and returning users do not see a theme flash.
        try
        {
            AppThemeResources.Apply(AppThemeResources.LoadSavedIsDarkTheme());
        }
        catch
        {
            // Theme loading must not block startup.
        }

        // Bắt tất cả unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (_isDisplayingError) return;
            _isDisplayingError = true;

            var ex = args.ExceptionObject as Exception;
            WriteExceptionLog("fatal_error.txt", "Unhandled Exception", ex);
            ShutdownSafely();
        };

        DispatcherUnhandledException += (s, args) =>
        {
            if (_isDisplayingError)
            {
                args.Handled = true;
                ShutdownSafely();
                return;
            }

            _isDisplayingError = true;
            WriteExceptionLog("ui_error.txt", "Dispatcher Exception", args.Exception);
            args.Handled = true;
            ShutdownSafely();
        };

        // Hiển thị cửa sổ sớm nhất có thể; các tác vụ chậm chạy sau để app không bị "ẩn".
        var splash = new SplashWindow();
        TraceStartup("Splash constructed");
        System.Windows.Application.Current.MainWindow = splash;
        splash.Show();
        TraceStartup("Splash shown");
        splash.Activate();
        TraceStartup("Splash activated");

        _ = InitializeAndShowLoginAsync(splash);
        TraceStartup("InitializeAndShowLoginAsync scheduled");
    }

    private async Task InitializeAndShowLoginAsync(SplashWindow splash)
    {
        try
        {
            await InitializeApplicationAsync(splash).WaitAsync(TimeSpan.FromSeconds(30));
            TraceStartup("InitializeApplicationAsync completed");
            if (DatabaseReadyTask != null)
            {
                try { await DatabaseReadyTask.WaitAsync(TimeSpan.FromSeconds(30)); }
                catch { }
            }
            splash.UpdateStatus("Đang mở cửa sổ đăng nhập...");

            var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
            TraceStartup("LoginWindow resolved");
            System.Windows.Application.Current.MainWindow = loginWindow;
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            loginWindow.WindowState = WindowState.Normal;
            loginWindow.Visibility = Visibility.Visible;
            loginWindow.ShowInTaskbar = true;
            loginWindow.Show();
            TraceStartup("LoginWindow shown");
            loginWindow.Activate();
            loginWindow.Focus();
            splash.CloseSplashCompletely();
            TraceStartup("Splash closed");
        }
        catch (Exception ex)
        {
            var errorLog = $"Startup Error: {DateTime.Now}\n" +
                           $"Message: {ex.Message}\n" +
                           $"StackTrace: {ex.StackTrace}\n" +
                           $"InnerException: {ex.InnerException?.Message}\n" +
                           $"InnerStackTrace: {ex.InnerException?.StackTrace}";
            WriteExceptionLog("startup_error.txt", "Startup Error", ex);
            File.WriteAllText("startup_error_details.txt", errorLog);
            splash.CloseSplashCompletely();
            MessageBox.Show($"Không thể khởi động ứng dụng:\n\n{ex.Message}\n\nChi tiết: startup_error_details.txt",
                "Lỗi khởi động", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    /// <summary>
    /// Khởi tạo ứng dụng trong background thread
    /// </summary>
    private async Task InitializeApplicationAsync(SplashWindow splash)
    {
        // Bước 1: Cấu hình Services (nhanh)
        splash.UpdateStatus("Đang cấu hình dịch vụ...");
        await Task.Delay(100); // Cho UI cập nhật
        
        var services = new ServiceCollection();
        ConfigureServices(services);
        TraceStartup("Services configured");
        ServiceProvider = services.BuildServiceProvider();
        TraceStartup("ServiceProvider built");

        // Bước 2: Khởi tạo Database trong nền để login luôn hiện, kể cả khi SQLite gặp lỗi.
        splash.UpdateStatus("Đang chuẩn bị cơ sở dữ liệu...");
        DatabaseReadyTask = Task.Run(InitializeDatabaseAsync);
        TraceStartup("Database task started");

        // Bước 3: Hoàn tất
        splash.UpdateStatus("Sẵn sàng đăng nhập...");
        await Task.Delay(200); // Cho user thấy hoàn tất
    }

    /// <summary>
    /// Khởi tạo database với async operations
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Tạo database (async)
        await dbContext.Database.EnsureCreatedAsync();
        
        // Tạo Users table nếu chưa có
        await dbContext.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                FullName TEXT,
                Phone TEXT,
                AvatarPath TEXT,
                Role INTEGER NOT NULL,
                CreatedDate TEXT NOT NULL
            );");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS InitialDocumentChecklistItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DocumentId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Status TEXT,
                ReceivedDate TEXT,
                FilePath TEXT,
                Note TEXT,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY(DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
            );");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS GuaranteeLetters (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DocumentId INTEGER NOT NULL,
                RelatedAppendixId INTEGER NULL,
                RelatedAppendixLabel TEXT NULL,
                ParentGuaranteeId INTEGER NULL,
                Type TEXT NOT NULL,
                LetterNumber TEXT NOT NULL,
                IssuingBank TEXT NOT NULL,
                GuaranteeValue REAL NOT NULL,
                IssueDate TEXT NOT NULL,
                EffectiveDate TEXT NOT NULL,
                ExpiryDate TEXT NOT NULL,
                LifecycleStatus TEXT NOT NULL,
                Notes TEXT NULL,
                PdfFilePath TEXT NOT NULL,
                CreatedDate TEXT NOT NULL,
                ModifiedDate TEXT NULL,
                FOREIGN KEY(DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE,
                FOREIGN KEY(RelatedAppendixId) REFERENCES ContractAppendices(Id) ON DELETE SET NULL,
                FOREIGN KEY(ParentGuaranteeId) REFERENCES GuaranteeLetters(Id) ON DELETE RESTRICT
            );");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS IX_GuaranteeLetters_ParentGuaranteeId
            ON GuaranteeLetters (ParentGuaranteeId);");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS IX_GuaranteeLetters_DocumentId_RelatedAppendixId
            ON GuaranteeLetters (DocumentId, RelatedAppendixId);");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS IX_GuaranteeLetters_DocumentId_LifecycleStatus_ExpiryDate
            ON GuaranteeLetters (DocumentId, LifecycleStatus, ExpiryDate);");

        // Chạy migrations trong 1 batch (nhanh hơn)
        await RunMigrationsAsync(dbContext);
    }

    /// <summary>
    /// Chạy tất cả migrations trong 1 transaction
    /// </summary>
    private async Task RunMigrationsAsync(AppDbContext dbContext)
    {
        var migrations = new (string TableName, string ColumnName, string Definition)[]
        {
            ("Documents", "Location", "TEXT"),
            ("Documents", "InvestorName", "TEXT"),
            ("Documents", "EmployerName", "TEXT"),
            ("Documents", "ContractorName", "TEXT"),
            ("Documents", "EmployerCode", "TEXT"),
            ("Documents", "ContractorCode", "TEXT"),
            ("Documents", "PlannedAdvanceValue", "REAL DEFAULT 0"),
            ("Documents", "IsCompleted", "INTEGER DEFAULT 0"),
            ("ContractAppendices", "PlannedAdvanceValue", "REAL DEFAULT 0"),
            ("AdvancePayments", "RelatedAppendixId", "INTEGER")
        };

        // Chạy tất cả migrations trong 1 transaction (nhanh hơn)
        using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            foreach (var migration in migrations)
            {
                try
                {
                if (!await ColumnExistsAsync(dbContext, migration.TableName, migration.ColumnName))
                {
                    var sql = $"ALTER TABLE {QuoteIdentifier(migration.TableName)} ADD COLUMN {QuoteIdentifier(migration.ColumnName)} {migration.Definition}";
                    await dbContext.Database.ExecuteSqlRawAsync(sql);
                }
                }
                catch 
                { 
                    // Column đã tồn tại, bỏ qua
                }
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<bool> ColumnExistsAsync(AppDbContext dbContext, string tableName, string columnName)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName.Replace("\"", "\"\"")}\")";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    private static string ResolveDatabasePath()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DocumentManagement");

        try
        {
            Directory.CreateDirectory(appDataPath);
            var probePath = Path.Combine(appDataPath, ".write-test");
            using (File.Open(probePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) { }
            File.Delete(probePath);
            return Path.Combine(appDataPath, "documents.db");
        }
        catch
        {
            var fallbackPath = Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(fallbackPath);
            return Path.Combine(fallbackPath, "documents.db");
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Database - lưu trong AppData
        var dbPath = ResolveDatabasePath();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}")
                   .EnableServiceProviderCaching()); // Cache provider để tăng tốc

        // Essential Services
        services.AddSingleton(new HttpClient());
        services.AddSingleton<IAppUpdateService, AppUpdateService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IGuaranteeService, GuaranteeService>();
        services.AddSingleton<GuaranteeFileService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<VtdvExportService>();
        services.AddSingleton<HsplExportService>();
        services.AddSingleton<ConstructionDirectiveService>();
        services.AddTransient<ConstructionDirectivesViewModel>();
        services.AddTransient<ConstructionDirectivesWindow>();

        // ViewModels (không singleton để tránh load data sớm)
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ModuleSelectionViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<AcceptanceViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<CvxdWindowViewModel>();
        services.AddTransient<VtdvViewModel>();
        services.AddTransient<HsplViewModel>();
        services.AddTransient<UserProfileViewModel>();
        services.AddTransient<AiChatViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddTransient<LoginWindow>();
        services.AddTransient<ModuleSelectionWindow>();
        services.AddTransient<ShellWindow>();
        services.AddTransient<AcceptanceWindow>();
        services.AddTransient<CvxdWindow>();
        services.AddTransient<UserProfileWindow>();
        services.AddTransient<DashboardView>();
        services.AddTransient<CvxdView>();
        services.AddTransient<VtdvView>();
        services.AddTransient<HsplView>();
    }
    }
}
