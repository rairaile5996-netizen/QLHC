using System;
using System.Windows;
using System.Windows.Input;
using DocumentManagement.Services;
using DocumentManagement.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.Views
{
    public partial class ShellWindow : Window
    {
        private readonly IAppUpdateService _appUpdateService;

        public ShellWindow(
            ShellViewModel viewModel,
            IAppUpdateService appUpdateService)
        {
            InitializeComponent();

            DataContext = viewModel;

            _appUpdateService = appUpdateService;

            Utilities.AppThemeResources.Apply(
                Resources,
                Utilities.AppThemeResources.LoadSavedIsDarkTheme());

            SourceInitialized += (_, _) =>
                ApplyWorkAreaBounds();

            Activated += ShellWindow_Activated;
        }

        private void ShellWindow_Activated(
            object? sender,
            EventArgs e)
        {
            _appUpdateService.SetActiveHost(UpdateHost);
        }

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

        private void BackButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // Show module selection window
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

            // Close current window
            Close();
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MinimizeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState =
                WindowState.Minimized;
        }
    }
}
