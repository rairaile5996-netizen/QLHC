using System.Windows;
using DocumentManagement.Utilities;
using DocumentManagement.ViewModels;

namespace DocumentManagement.Views
{
    public partial class AcceptanceWindow : Window
    {
        public AcceptanceWindow(AcceptanceViewModel viewModel)
        {
            InitializeComponent();
            AppThemeResources.Apply(Resources, AppThemeResources.LoadSavedIsDarkTheme());
            DataContext = viewModel;

            SourceInitialized += (_, _) => ApplyWorkAreaBounds();
        }

        private void ApplyWorkAreaBounds()
        {
            var workArea = SystemParameters.WorkArea;
            WindowState = WindowState.Normal;
            Left = workArea.Left;
            Top = workArea.Top;
            Width = workArea.Width;
            Height = workArea.Height;
            MaxWidth = workArea.Width;
            MaxHeight = workArea.Height;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }
}
