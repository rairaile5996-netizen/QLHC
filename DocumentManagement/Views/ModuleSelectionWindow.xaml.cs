using System;
using System.Windows;
using System.Windows.Input;
using DocumentManagement.Services;
using DocumentManagement.Utilities;
using DocumentManagement.ViewModels;

namespace DocumentManagement.Views
{
    public partial class ModuleSelectionWindow : Window
    {
        private readonly ModuleSelectionViewModel _viewModel;
        private readonly IAppUpdateService _appUpdateService;

        public ModuleSelectionWindow(
            ModuleSelectionViewModel viewModel,
            IAppUpdateService appUpdateService)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _appUpdateService = appUpdateService;

            Topmost = false;

            AppThemeResources.Apply(
                Resources,
                AppThemeResources.LoadSavedIsDarkTheme());

            DataContext = viewModel;

            Loaded += (_, _) => WindowState = WindowState.Maximized;

            Activated += ModuleSelectionWindow_Activated;
        }

        private void ModuleSelectionWindow_Activated(
            object? sender,
            EventArgs e)
        {
            _appUpdateService.SetActiveHost(UpdateHost);
        }

        private int _lastCardColumnCount;

        private void ModuleSelectionWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var width = e.NewSize.Width;
            var columns = width >= 1080 ? 3 : width >= 760 ? 2 : 1;
            if (columns == _lastCardColumnCount) return;
            _lastCardColumnCount = columns;
            ModuleCardsPanel.Columns = columns;
        }
        private void ContractManagement_Click(
            object sender,
            MouseButtonEventArgs e)
        {
            _viewModel
                .SelectContractManagementCommand
                .Execute(null);
        }

        private void AcceptanceDocuments_Click(
            object sender,
            MouseButtonEventArgs e)
        {
            _viewModel
                .SelectAcceptanceDocumentsCommand
                .Execute(null);
        }

        private void ModuleSelectionWindow_PreviewMouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (!_viewModel.IsGuaranteeNotificationOpen ||
                e.OriginalSource is not System.Windows.Media.Visual source)
            {
                return;
            }

            if (!source.IsDescendantOf(GuaranteeNotificationHost) &&
                !source.IsDescendantOf(GuaranteeNotificationButton))
            {
                _viewModel.IsGuaranteeNotificationOpen = false;
            }
        }
    }
}
