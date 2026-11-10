using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.ViewModels
{
    public partial class ShellViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private MainViewModel? _mainViewModel;

        [ObservableProperty]
        private object? _currentView;

        public ShellViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Khởi tạo MainViewModel và navigate đến Dashboard
        /// </summary>
        public async Task InitializeAndNavigateToDashboardAsync()
        {
            if (_mainViewModel == null)
            {
                _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                await _mainViewModel.InitializeAsync();
            }
            
            CurrentView = _mainViewModel;
        }

        public async Task<bool> InitializeAndNavigateToGuaranteeAsync(int documentId, int guaranteeId)
        {
            if (_mainViewModel == null)
            {
                _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                await _mainViewModel.InitializeAsync();
            }

            CurrentView = _mainViewModel;
            return await _mainViewModel.NavigateToGuaranteeAsync(documentId, guaranteeId);
        }

        /// <summary>
        /// Khởi tạo MainViewModel (để có data) và navigate đến CVXD
        /// </summary>
        public async Task InitializeAndNavigateToCvxdAsync()
        {
            // Khởi tạo MainViewModel trước để load data
            if (_mainViewModel == null)
            {
                _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                await _mainViewModel.InitializeAsync();
            }
            
            // Navigate đến CVXD
            NavigateToCvxd();
        }

        [RelayCommand]
        public void NavigateToDashboard()
        {
            if (_mainViewModel != null)
            {
                CurrentView = _mainViewModel;
            }
        }

        [RelayCommand]
        public void NavigateToCvxd()
        {
            CurrentView = _serviceProvider.GetRequiredService<CvxdWindowViewModel>();
        }

        [RelayCommand]
        public void NavigateToVtdv()
        {
            var vm = _serviceProvider.GetRequiredService<VtdvViewModel>();
            vm.RefreshContext();
            CurrentView = vm;
        }
    }
}
