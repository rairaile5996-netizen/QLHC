using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DocumentManagement.Services;
using DocumentManagement.Utilities;
using DocumentManagement.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.Views
{
    public partial class CvxdWindow : Window
    {
        private readonly IAppUpdateService _appUpdateService;

        private bool _skipUnsavedPrompt;

        private const int GwlStyle = -16;
        private const int WsSysmenu = 0x00080000;
        private const int WsMinimizebox = 0x00020000;
        private const int WmSyscommand = 0x0112;
        private const int ScMinimize = 0xF020;


        [DllImport("user32.dll")]
        private static extern int GetWindowLong(
            IntPtr hWnd,
            int nIndex);


        [DllImport("user32.dll")]
        private static extern int SetWindowLong(
            IntPtr hWnd,
            int nIndex,
            int dwNewLong);


        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam);


        public CvxdWindow(
            CvxdWindowViewModel viewModel,
            IAppUpdateService appUpdateService)
        {
            InitializeComponent();

            _appUpdateService = appUpdateService;

            AppThemeResources.Apply(
                Resources,
                AppThemeResources.LoadSavedIsDarkTheme());

            DataContext = viewModel;


            // Đảm bảo cửa sổ không che thanh taskbar
            // khi ở chế độ WindowStyle=None.
            SourceInitialized += (s, e) =>
            {
                ApplyWorkAreaBounds();
                EnableNativeMinimize();
            };


            Activated += CvxdWindow_Activated;

            Closing += CvxdWindow_Closing;
        }


        // =========================================================
        // UPDATE HOST
        // =========================================================

        private void CvxdWindow_Activated(
            object? sender,
            EventArgs e)
        {
            _appUpdateService.SetActiveHost(UpdateHost);
        }


        // =========================================================
        // CLOSING
        // =========================================================

        private void CvxdWindow_Closing(
            object? sender,
            System.ComponentModel.CancelEventArgs e)
        {
            if (_skipUnsavedPrompt)
            {
                return;
            }


            if (!ConfirmUnsavedChanges())
            {
                e.Cancel = true;
            }
        }


        // =========================================================
        // UNSAVED CHANGES
        // =========================================================

        private bool ConfirmUnsavedChanges()
        {
            if (DataContext is not CvxdWindowViewModel vm ||
                !vm.IsDirty)
            {
                return true;
            }


            var result =
                CvxdMessageBox.Show(
                    "Cảnh báo thoát",
                    "Bạn có thay đổi chưa lưu. Bạn có muốn lưu trước khi đóng không?",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);


            if (result == MessageBoxResult.Cancel)
            {
                return false;
            }


            if (result == MessageBoxResult.Yes)
            {
                vm.SaveDataCommand.Execute(null);
            }


            return true;
        }


        // =========================================================
        // BACK
        // =========================================================

        private void BackButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!ConfirmUnsavedChanges())
            {
                return;
            }


            // Show module selection window.
            var serviceProvider =
                App.ServiceProvider;


            var moduleSelectionWindow =
                serviceProvider
                    .GetRequiredService<ModuleSelectionWindow>();


            Application.Current.MainWindow =
                moduleSelectionWindow;


            moduleSelectionWindow.WindowState = WindowState.Normal;
            moduleSelectionWindow.ShowInTaskbar = true;
            moduleSelectionWindow.Show();
            moduleSelectionWindow.Activate();
            moduleSelectionWindow.Focus();


            _skipUnsavedPrompt = true;


            Close();
        }


        // =========================================================
        // MINIMIZE
        // =========================================================

        private void MinimizeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var handle =
                new WindowInteropHelper(this)
                    .Handle;


            if (handle != IntPtr.Zero)
            {
                SendMessage(
                    handle,
                    WmSyscommand,
                    new IntPtr(ScMinimize),
                    IntPtr.Zero);
            }
            else
            {
                SystemCommands.MinimizeWindow(this);
            }
        }


        // =========================================================
        // CLOSE
        // =========================================================

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }


        // =========================================================
        // NATIVE MINIMIZE
        // =========================================================

        private void EnableNativeMinimize()
        {
            var handle =
                new WindowInteropHelper(this)
                    .Handle;


            if (handle == IntPtr.Zero)
            {
                return;
            }


            var style =
                GetWindowLong(
                    handle,
                    GwlStyle);


            SetWindowLong(
                handle,
                GwlStyle,
                style |
                WsSysmenu |
                WsMinimizebox);
        }


        // =========================================================
        // WORK AREA
        // =========================================================

        private void ApplyWorkAreaBounds()
        {
            var workArea =
                SystemParameters.WorkArea;


            WindowState =
                WindowState.Normal;


            Left =
                workArea.Left;


            Top =
                workArea.Top;


            Width =
                workArea.Width;


            Height =
                workArea.Height;


            MaxWidth =
                workArea.Width;


            MaxHeight =
                workArea.Height;
        }


        // =========================================================
        // CVXD VIEW LOADED
        // =========================================================

        private void CvxdView_Loaded(
            object sender,
            RoutedEventArgs e)
        {
        }
    }
}
