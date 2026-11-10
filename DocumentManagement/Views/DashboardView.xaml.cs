using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using DocumentManagement.ViewModels;

namespace DocumentManagement.Views
{
    public partial class DashboardView : System.Windows.Controls.UserControl
    {
        private MainViewModel? _viewModel;

        // Windows API for dark title bar
        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public DashboardView()
        {
            InitializeComponent();
            
            DataContextChanged += (s, e) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    _viewModel = vm;
                    
                    // Auto-scroll chat when new messages arrive
                    _viewModel.ChatMessages.CollectionChanged -= ChatMessages_CollectionChanged;
                    _viewModel.ChatMessages.CollectionChanged += ChatMessages_CollectionChanged;
                }
            };

            // Keyboard shortcuts
            PreviewKeyDown += MainWindow_PreviewKeyDown;

            Loaded += MainWindow_Loaded;
        }

        private bool? _compactDashboard;
        private bool? _compactDashboardHeader;

        private void DashboardView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var compactHeader = e.NewSize.Width < 800;
            if (_compactDashboardHeader != compactHeader)
            {
                _compactDashboardHeader = compactHeader;
                Grid.SetRow(DashboardHeaderActions, compactHeader ? 1 : 0);
                Grid.SetColumn(DashboardHeaderActions, compactHeader ? 0 : 1);
                Grid.SetColumnSpan(DashboardHeaderActions, compactHeader ? 2 : 1);
                DashboardHeaderActions.Margin = compactHeader ? new Thickness(0, 12, 0, 0) : new Thickness(0);
            }

            var compact = e.NewSize.Width < 950;
            if (_compactDashboard == compact) return;
            _compactDashboard = compact;
            DashboardListColumn.MinWidth = compact ? 0 : 280;
            DashboardListColumn.MaxWidth = compact ? double.PositiveInfinity : 420;
            DashboardListColumn.Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(0.3, GridUnitType.Star);
            DashboardDetailColumn.Width = compact ? new GridLength(0) : new GridLength(0.7, GridUnitType.Star);
            DashboardListRow.Height = compact ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
            DashboardDetailRow.Height = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            DashboardListPanel.Height = compact ? 230 : double.NaN;
            DashboardListPanel.Margin = compact ? new Thickness(12, 0, 12, 12) : new Thickness(24, 0, 12, 24);
            Grid.SetRow(DashboardDetailPanel, compact ? 1 : 0);
            Grid.SetColumn(DashboardDetailPanel, compact ? 0 : 1);
            DashboardDetailPanel.Margin = compact ? new Thickness(12, 0, 12, 12) : new Thickness(12, 0, 24, 24);
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (_viewModel == null) return;

            // Ctrl+N: Thêm mới hợp đồng
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _viewModel.OpenAddDialogCommand.Execute(null);
                e.Handled = true;
            }
            // Ctrl+F: Focus vào ô tìm kiếm
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var searchBox = FindName("SearchBox") as TextBox;
                searchBox?.Focus();
                searchBox?.SelectAll();
                e.Handled = true;
            }
            // Delete: Xóa hợp đồng đang chọn
            else if (e.Key == Key.Delete && _viewModel.SelectedDocument != null)
            {
                // Chỉ xóa nếu không đang focus vào textbox
                if (Keyboard.FocusedElement is not TextBox)
                {
                    _viewModel.DeleteDocumentCommand.Execute(_viewModel.SelectedDocument);
                    e.Handled = true;
                }
            }
            // Ctrl+E: Sửa hợp đồng đang chọn
            else if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control && _viewModel.SelectedDocument != null)
            {
                _viewModel.OpenEditDialogCommand.Execute(_viewModel.SelectedDocument);
                e.Handled = true;
            }
            // Escape: Đóng các popup/panel
            else if (e.Key == Key.Escape)
            {
                if (_viewModel.IsUserMenuOpen) _viewModel.IsUserMenuOpen = false;
                else if (_viewModel.IsSettingsOpen) _viewModel.IsSettingsOpen = false;
                else if (_viewModel.IsNotificationOpen) _viewModel.IsNotificationOpen = false;
                else if (_viewModel.IsAiChatOpen) _viewModel.IsAiChatOpen = false;
            }
            // F5: Refresh danh sách
            else if (e.Key == Key.F5)
            {
                _viewModel.LoadDocumentsCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void ChatMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var scrollViewer = FindName("ChatScrollViewer") as ScrollViewer;
                    scrollViewer?.ScrollToEnd();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void DashboardView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel?.IsGuaranteeNotificationOpen != true || e.OriginalSource is not System.Windows.Media.Visual source)
            {
                return;
            }

            if (!source.IsDescendantOf(GuaranteeNotificationHost) &&
                !source.IsDescendantOf(GuaranteeNotificationButton))
            {
                _viewModel.IsGuaranteeNotificationOpen = false;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            // Setup custom toast from ViewModel
            SetupCustomToast();

            // Set toast action cho ErrorHandlingService
            _viewModel.SetupErrorHandlingToast();

            await _viewModel.LoadDocumentsCommand.ExecuteAsync(null);
        }

        private void SetupCustomToast()
        {
            if (_viewModel == null) return;
            // Subscribe to toast events from ViewModel
            _viewModel.ToastRequested -= ShowCustomToast;
            _viewModel.ToastRequested += ShowCustomToast;
        }

        public void ShowCustomToast(string message)
        {
            Dispatcher.Invoke(() =>
            {
                // TODO: Chuyển CustomToast lên ShellWindow hoặc implement Snackbar
                // Tạm thời hiển thị message box hoặc bỏ qua
                // MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void DropZone_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (_viewModel == null) return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    _viewModel.HandleFileDrop(files);
                }
            }
        }

        private void ChatInputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
                {
                    // Ctrl+Enter: xuống dòng
                    if (sender is TextBox textBox)
                    {
                        var caretIndex = textBox.CaretIndex;
                        textBox.Text = textBox.Text.Insert(caretIndex, Environment.NewLine);
                        textBox.CaretIndex = caretIndex + Environment.NewLine.Length;
                    }
                    e.Handled = true;
                }
                else
                {
                    // Enter: gửi tin nhắn
                    if (_viewModel != null && _viewModel.SendChatMessageCommand.CanExecute(null))
                    {
                        _viewModel.SendChatMessageCommand.Execute(null);
                    }
                    e.Handled = true;
                }
            }
        }



        private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = ValidationRegex().IsMatch(e.Text);
        }

        [GeneratedRegex("[^0-9]+")]
        private static partial Regex ValidationRegex();


    }
}

