using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using DocumentManagement.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.ViewModels
{
    /// <summary>
    /// ViewModel cho window Hồ sơ nghiệm thu (CVXD + VTDV)
    /// </summary>
    public partial class AcceptanceViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private MainViewModel? _mainViewModel; // Để share data
        private CvxdWindowViewModel? _cvxdViewModel;
        private VtdvViewModel? _vtdvViewModel;
        private HsplViewModel? _hsplViewModel;
        private PdvtViewModel? _pdvtViewModel;

        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private int _selectedTabIndex = 0; // 0: CVXD, 1: VTDV

        public AcceptanceViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Khởi tạo data từ MainViewModel và navigate đến CVXD
        /// </summary>
        public async Task InitializeAsync()
        {
            // Khởi tạo MainViewModel để load data hợp đồng
            _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            await _mainViewModel.InitializeAsync();
            
            // Navigate đến CVXD
            NavigateToCvxd();
        }

        [RelayCommand]
        public void NavigateToCvxd()
        {
            if (_cvxdViewModel == null && _mainViewModel != null)
            {
                // Tạo CvxdWindowViewModel với MainViewModel
                _cvxdViewModel = new CvxdWindowViewModel(_mainViewModel, _serviceProvider.GetRequiredService<IAuthService>());
            }
            
            CurrentView = _cvxdViewModel;
            SelectedTabIndex = 0;
        }

        [RelayCommand]
        public void NavigateToVtdv()
        {
            if (_vtdvViewModel == null)
            {
                _vtdvViewModel = _serviceProvider.GetRequiredService<VtdvViewModel>();
            }
            
            _vtdvViewModel.RefreshContext();
            CurrentView = _vtdvViewModel;
            SelectedTabIndex = 1;
        }

        [RelayCommand]
        public void NavigateToHspl()
        {
            if (_hsplViewModel == null && _mainViewModel != null)
            {
                _hsplViewModel = new HsplViewModel(
                    _mainViewModel,
                    _serviceProvider.GetRequiredService<HsplExportService>(),
                    () => System.IO.Path.Combine(AppContext.BaseDirectory, "Templates", "HSPL_templates"));
            }

            if (_hsplViewModel != null && _mainViewModel?.SelectedDocument != null)
                _hsplViewModel.SelectedDocument = _mainViewModel.SelectedDocument;

            CurrentView = _hsplViewModel;
            SelectedTabIndex = 2;
        }

        [RelayCommand]
        public void NavigateToPdvt()
        {
            if (_pdvtViewModel == null && _mainViewModel != null)
                _pdvtViewModel = new PdvtViewModel(_mainViewModel);
            CurrentView = _pdvtViewModel;
            SelectedTabIndex = 3;
        }

        /// <summary>
        /// Lấy MainViewModel để các view con có thể access data
        /// </summary>
        public MainViewModel? GetMainViewModel() => _mainViewModel;
    }
}
