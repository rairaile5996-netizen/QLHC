using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Linq;
using DocumentManagement.ViewModels;
using DocumentManagement.Models;
using DocumentManagement.Utilities;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;

namespace DocumentManagement.Views
{

    public partial class CvxdView : UserControl
    {
        private void OpenExportedFiles_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CvxdWindowViewModel vm || vm.SelectedDocument == null)
            {
                MessageBox.Show(Window.GetWindow(this), "Vui lòng chọn hợp đồng trước.", "Hồ sơ đã tạo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(vm.SelectedDocument.FolderPath))
            {
                MessageBox.Show(Window.GetWindow(this), "Hợp đồng chưa có thư mục lưu hồ sơ.", "Hồ sơ đã tạo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var window = new ExportedFilesWindow(vm.SelectedDocument.ContractNumber ?? "", vm.SelectedDocument.FolderPath)
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
        }

        private void FindReplaceCvxd_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CvxdWindowViewModel vm)
                new FindReplaceWindow(vm, false, (find, advance) => FocusCvxdFindMatch(vm, find, advance)) { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control ||
                (e.Key != Key.F && e.Key != Key.H)) return;
            if (DataContext is not CvxdWindowViewModel vm) return;

            e.Handled = true;
            var window = new FindReplaceWindow(vm, false, (find, advance) => FocusCvxdFindMatch(vm, find, advance))
            { Owner = Window.GetWindow(this) };
            window.Show();
            if (e.Key == Key.H) window.FocusReplaceInput();
        }

        private bool? _compactSidebarBand;
        private bool? _compactContractBar;
        private bool? _compactCvxdToolbar;

        private void MainContentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTabViewportHeights();
        }

        private void UpdateTabViewportHeights()
        {
            if (MainContentScrollViewer == null || CvxdContractHeader == null ||
                CvxdTabViewport == null || VtdvTabViewport == null || ProjectInfoTabViewport == null ||
                HsplTabViewport == null || PdvtTabViewport == null) return;

            // The outer ScrollViewer measures its content with unlimited height. Give tabs
            // with their own scrolling controls a real viewport so the table/panel scrolls
            // independently instead of extending the whole page.
            var availableHeight = MainContentScrollViewer.ActualHeight - CvxdContractHeader.ActualHeight;
            if (availableHeight <= 0) return;

            CvxdTabViewport.MaxHeight = availableHeight;
            VtdvTabViewport.MaxHeight = availableHeight;
            ProjectInfoTabViewport.MaxHeight = availableHeight;
            HsplTabViewport.Height = availableHeight;
            PdvtTabViewport.Height = availableHeight;
        }

        private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Keep action groups together. At laptop widths the editing tools occupy
            // the first row and save/import/export form one intentional second row.
            var compactToolbar = e.NewSize.Width < 1550;
            if (_compactCvxdToolbar != compactToolbar)
            {
                _compactCvxdToolbar = compactToolbar;
                Grid.SetColumnSpan(CvxdEditActions, compactToolbar ? 3 : 1);
                Grid.SetRow(CvxdDataActions, compactToolbar ? 1 : 0);
                Grid.SetColumn(CvxdDataActions, compactToolbar ? 0 : 1);
                Grid.SetRow(CvxdExportActions, compactToolbar ? 1 : 0);
                Grid.SetColumn(CvxdExportActions, compactToolbar ? 1 : 2);
            }

            var compact = e.NewSize.Width < 1180;
            if (_compactSidebarBand != compact)
            {
                _compactSidebarBand = compact;
                SidebarCompactToggle.IsChecked = compact;
            }

            var compactContractBar = e.NewSize.Width < 900;
            if (_compactContractBar == compactContractBar) return;
            _compactContractBar = compactContractBar;
            CvxdContractSelector.Width = compactContractBar ? 250 : 360;
            Grid.SetRow(CvxdOpenExportedFilesButton, compactContractBar ? 2 : 0);
            Grid.SetColumn(CvxdOpenExportedFilesButton, compactContractBar ? 0 : 1);
            CvxdOpenExportedFilesButton.HorizontalAlignment = compactContractBar ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
            CvxdOpenExportedFilesButton.Margin = compactContractBar ? new Thickness(0, 8, 0, 0) : new Thickness(16, 0, 0, 0);

            OtherTemplatesSideColumn.Width = compactContractBar ? new GridLength(0) : new GridLength(300);
            OtherTemplatesHeaderRow.Height = compactContractBar ? GridLength.Auto : new GridLength(50);
            Grid.SetRow(OtherTemplatesSortActions, compactContractBar ? 1 : 0);
            Grid.SetColumn(OtherTemplatesSortActions, compactContractBar ? 0 : 1);
            Grid.SetColumnSpan(OtherTemplatesSortActions, compactContractBar ? 2 : 1);
            OtherTemplatesSortActions.HorizontalAlignment = compactContractBar ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            OtherTemplatesSortActions.Margin = compactContractBar ? new Thickness(12, 4, 0, 8) : new Thickness(0, 0, 12, 0);
            Grid.SetRow(OtherTemplatesSelectedPanel, compactContractBar ? 3 : 0);
            Grid.SetColumn(OtherTemplatesSelectedPanel, compactContractBar ? 0 : 1);
            Grid.SetRowSpan(OtherTemplatesSelectedPanel, compactContractBar ? 1 : 3);
        }

        private void FindReplaceVtdv_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CvxdWindowViewModel vm)
                new FindReplaceWindow(vm, true, (find, advance) => FocusVtdvFindMatch(vm, find, advance)) { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        private void FocusVtdvFindMatch(CvxdWindowViewModel vm, string find, bool advance)
        {
            var item = vm.VtdvViewModel?.FindNextItem(find, advance);
            if (item == null) return;
            vm.VtdvViewModel!.SelectedVtdvItem = item;
            DataGridVtdvItemsEmbedded.ScrollIntoView(item);
        }

        private void FocusCvxdFindMatch(CvxdWindowViewModel vm, string find, bool advance)
        {
            var item = vm.FindNextCvxdItem(find, advance);
            if (item == null) return;
            vm.SelectedCvxdItem = item;
            DataGridCvxd.SelectedItem = item;
            DataGridCvxd.ScrollIntoView(item);
        }

        private bool _isSyncingOtherTemplateSelection;
        private bool _otherTemplateSortDescending;

        private void OtherTemplatesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingOtherTemplateSelection) return;

            if (DataContext is CvxdWindowViewModel vm && sender is ListBox listBox)
            {
                _isSyncingOtherTemplateSelection = true;
                try
                {
                    vm.SetSelectedOtherTemplates(listBox.SelectedItems.OfType<OtherTemplateItem>());
                }
                finally
                {
                    _isSyncingOtherTemplateSelection = false;
                }
                SyncOtherTemplateSelectAllCheckBox();
            }
        }

        private void OtherTemplateAllTab_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CvxdWindowViewModel vm) return;
            vm.SetOtherTemplateSelectedFilter(false);
            OtherTemplateAllTab.IsChecked = true;
            OtherTemplateSelectedTab.IsChecked = false;
            OtherTemplateAllUnderline.Visibility = Visibility.Visible;
            OtherTemplateSelectedUnderline.Visibility = Visibility.Collapsed;
            SyncOtherTemplateSelectAllCheckBox();
        }

        private void OtherTemplateSelectedTab_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CvxdWindowViewModel vm) return;
            vm.SetOtherTemplateSelectedFilter(true);
            OtherTemplateAllTab.IsChecked = false;
            OtherTemplateSelectedTab.IsChecked = true;
            OtherTemplateAllUnderline.Visibility = Visibility.Collapsed;
            OtherTemplateSelectedUnderline.Visibility = Visibility.Visible;
            SyncOtherTemplateSelectAllCheckBox();
        }

        private void OtherTemplateSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox || OtherTemplatesListBox == null) return;

            _isSyncingOtherTemplateSelection = true;
            try
            {
                if (checkBox.IsChecked == true)
                {
                    OtherTemplatesListBox.SelectAll();
                }
                else
                {
                    OtherTemplatesListBox.UnselectAll();
                }
            }
            finally
            {
                _isSyncingOtherTemplateSelection = false;
            }

            if (DataContext is CvxdWindowViewModel vm)
            {
                vm.SetSelectedOtherTemplates(OtherTemplatesListBox.SelectedItems.OfType<OtherTemplateItem>());
            }
        }

        private void RemoveSelectedOtherTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not OtherTemplateItem template)
                return;

            _isSyncingOtherTemplateSelection = true;
            try
            {
                // SelectedItems is the source of truth even when virtualization has
                // recycled the row container for an off-screen template.
                OtherTemplatesListBox.SelectedItems.Remove(template);
                if (OtherTemplatesListBox.ItemContainerGenerator.ContainerFromItem(template) is ListBoxItem item)
                    item.IsSelected = false;

                if (DataContext is CvxdWindowViewModel vm)
                    vm.SetSelectedOtherTemplates(OtherTemplatesListBox.SelectedItems.OfType<OtherTemplateItem>());
            }
            finally
            {
                _isSyncingOtherTemplateSelection = false;
            }

            SyncOtherTemplateSelectAllCheckBox();
        }

        private void OtherTemplateItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBoxItem item || OtherTemplatesListBox == null) return;
            if (FindParent<TextBox>(e.OriginalSource as DependencyObject ?? item) != null) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;

            if (item.IsSelected)
                OtherTemplatesListBox.SelectedItems.Remove(item.DataContext);
            else
                OtherTemplatesListBox.SelectedItems.Add(item.DataContext);

            e.Handled = true;
        }

        private void OtherTemplateSort_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CvxdWindowViewModel vm) return;
            _otherTemplateSortDescending = !_otherTemplateSortDescending;
            vm.SetOtherTemplateSort(_otherTemplateSortDescending);
            OtherTemplateSortIcon.Kind = _otherTemplateSortDescending
                ? MaterialDesignThemes.Wpf.PackIconKind.SortAlphabeticalDescending
                : MaterialDesignThemes.Wpf.PackIconKind.SortAlphabeticalAscending;
            OtherTemplateSortText.Text = _otherTemplateSortDescending ? "Tên Z - A" : "Tên A - Z";
        }

        private void OtherTemplateSearchClear_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CvxdWindowViewModel vm)
            {
                vm.OtherTemplateSearch = string.Empty;
            }
        }

        private void OtherTemplateListLayout_Click(object sender, RoutedEventArgs e)
        {
            OtherTemplateListLayoutToggle.IsChecked = true;
            OtherTemplateGridLayoutToggle.IsChecked = false;
        }

        private void OtherTemplateGridLayout_Click(object sender, RoutedEventArgs e)
        {
            OtherTemplateListLayoutToggle.IsChecked = false;
            OtherTemplateGridLayoutToggle.IsChecked = true;
        }

        private void SyncOtherTemplateSelectAllCheckBox()
        {
            if (OtherTemplateSelectAllCheckBox == null || OtherTemplatesListBox == null) return;

            _isSyncingOtherTemplateSelection = true;
            try
            {
                var visibleCount = OtherTemplatesListBox.Items.Count;
                var selectedCount = OtherTemplatesListBox.SelectedItems.Count;
                OtherTemplateSelectAllCheckBox.IsChecked = visibleCount > 0 && selectedCount == visibleCount;
            }
            finally
            {
                _isSyncingOtherTemplateSelection = false;
            }
        }

        public CvxdView()
        {
            InitializeComponent();
            Loaded += (_, _) => AttachCustomColumnHandlers();
            Loaded += (_, _) => UpdateTabViewportHeights();
            DataContextChanged += (_, _) => AttachCustomColumnHandlers();
        }

        private CvxdWindowViewModel? _attachedVm;
        private CellEditSnapshot? _activeEditSnapshot;
        private readonly Dictionary<DataGrid, DataGridCellInfo> _selectionAnchors = new();
        private readonly Dictionary<DataGrid, DataGridCellInfo> _dragSelectionAnchors = new();
        private readonly Dictionary<ComboBox, ComboBoxEditState> _comboBoxEditStates = new();
        private DataGridColumn? _vtdvPasteColumn;
        private DataGridColumnHeader? _vtdvPasteHeader;

        private void ClearVtdvPasteColumn()
        {
            _vtdvPasteHeader?.ClearValue(Control.BackgroundProperty);
            _vtdvPasteHeader?.ClearValue(Control.ForegroundProperty);
            _vtdvPasteHeader = null;
            _vtdvPasteColumn = null;
        }

        private sealed record CellEditSnapshot(object Item, DataGridColumn Column, string? PropertyName, string? CustomFieldName, object? OriginalValue);
        private sealed record ComboBoxEditState(DataGridCellInfo Cell, object? OriginalValue);

        private void AttachCustomColumnHandlers()
        {
            if (_attachedVm != null)
            {
                _attachedVm.PropertyChanged -= ViewModel_PropertyChanged;
                _attachedVm.CvxdInfo.CustomColumns.CollectionChanged -= CustomColumns_CollectionChanged;
                _attachedVm.CvxdInfo.VtdvCustomColumns.CollectionChanged -= CustomColumns_CollectionChanged;
                if (_attachedVm.VtdvViewModel != null)
                {
                    _attachedVm.VtdvViewModel.PropertyChanged -= VtdvViewModel_PropertyChanged;
                }
            }

            _attachedVm = DataContext as CvxdWindowViewModel;
            if (_attachedVm != null)
            {
                _attachedVm.PropertyChanged += ViewModel_PropertyChanged;
                _attachedVm.CvxdInfo.CustomColumns.CollectionChanged += CustomColumns_CollectionChanged;
                _attachedVm.CvxdInfo.VtdvCustomColumns.CollectionChanged += CustomColumns_CollectionChanged;
                if (_attachedVm.VtdvViewModel != null)
                {
                    _attachedVm.VtdvViewModel.PropertyChanged += VtdvViewModel_PropertyChanged;
                }
            }

            RefreshCustomColumns();
            RefreshVtdvCustomColumns();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CvxdWindowViewModel.CvxdInfo))
            {
                AttachCustomColumnHandlers();
            }
        }

        private void VtdvViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VtdvViewModel.SelectedSession))
            {
                ClearVtdvPasteColumn();
                RefreshVtdvCustomColumns();
            }
        }

        private async void VtdvExportWord_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is VtdvViewModel vm)
            {
                await vm.ExportWordCommand.ExecuteAsync(null);
                return;
            }

            CvxdMessageBox.Show("Không thể xuất Word", "Không tìm thấy dữ liệu VTĐV để thực hiện thao tác.",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void CustomColumns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshCustomColumns();
            RefreshVtdvCustomColumns();
        }

        private void RefreshCustomColumns()
        {
            if (DataGridCvxd == null || DataContext is not CvxdWindowViewModel vm) return;

            for (int i = DataGridCvxd.Columns.Count - 1; i >= 0; i--)
            {
                if (GetColumnTag(DataGridCvxd.Columns[i]) is string tag && tag.StartsWith("custom:", StringComparison.Ordinal))
                {
                    DataGridCvxd.Columns.RemoveAt(i);
                }
            }

            foreach (var item in vm.CvxdItems)
            {
                var orderedFields = new ObservableCollection<CvxdCustomFieldValue>();
                foreach (var customColumn in vm.CvxdInfo.CustomColumns)
                {
                    var field = item.CustomFields.FirstOrDefault(f => string.Equals(f.Name, customColumn.Name, StringComparison.OrdinalIgnoreCase));
                    field ??= new CvxdCustomFieldValue { Name = customColumn.Name };
                    orderedFields.Add(field);
                }
                item.CustomFields = orderedFields;
            }

            foreach (var customColumn in vm.CvxdInfo.CustomColumns.Where(c => !string.IsNullOrWhiteSpace(c.Name)))
            {
                var column = new DataGridTextColumn
                {
                    Header = CreateColumnHeader(customColumn.Name, vm.RenameCvxdCustomColumnCommand, vm.DeleteCvxdCustomColumnCommand),
                    Width = 150,
                    Binding = new Binding($"CustomFields[{GetCustomColumnIndex(vm, customColumn.Name)}].Value"),
                    ElementStyle = (Style)FindResource("CvxdCellTextBlockWrapStyle"),
                    EditingElementStyle = (Style)FindResource("CvxdCellEditingWrapStyle")
                };
                column.SetValue(FrameworkElement.TagProperty, "custom:" + customColumn.Name);

                DataGridCvxd.Columns.Add(column);
            }
        }

        private static int GetCustomColumnIndex(CvxdWindowViewModel vm, string name)
        {
            for (int i = 0; i < vm.CvxdInfo.CustomColumns.Count; i++)
            {
                if (string.Equals(vm.CvxdInfo.CustomColumns[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        private void RefreshVtdvCustomColumns()
        {
            if (DataGridVtdvItemsEmbedded == null || DataContext is not CvxdWindowViewModel vm || vm.VtdvViewModel == null) return;

            for (int i = DataGridVtdvItemsEmbedded.Columns.Count - 1; i >= 0; i--)
            {
                if (GetColumnTag(DataGridVtdvItemsEmbedded.Columns[i]) is string tag && tag.StartsWith("vtdv-custom:", StringComparison.Ordinal))
                {
                    DataGridVtdvItemsEmbedded.Columns.RemoveAt(i);
                }
            }

            if (vm.VtdvViewModel.SelectedSession != null)
            {
                foreach (var item in vm.VtdvViewModel.SelectedSession.Items)
                {
                    var orderedFields = new ObservableCollection<CvxdCustomFieldValue>();
                    foreach (var customColumn in vm.CvxdInfo.VtdvCustomColumns)
                    {
                        var field = item.CustomFields.FirstOrDefault(f => string.Equals(f.Name, customColumn.Name, StringComparison.OrdinalIgnoreCase));
                        field ??= new CvxdCustomFieldValue { Name = customColumn.Name };
                        orderedFields.Add(field);
                    }
                    item.CustomFields = orderedFields;
                }
            }

            foreach (var customColumn in vm.CvxdInfo.VtdvCustomColumns.Where(c => !string.IsNullOrWhiteSpace(c.Name)))
            {
                var column = new DataGridTextColumn
                {
                    Header = CreateColumnHeader(customColumn.Name, vm.VtdvViewModel.RenameVtdvCustomColumnCommand, vm.VtdvViewModel.DeleteVtdvCustomColumnCommand),
                    Width = 150,
                    Binding = new Binding($"CustomFields[{GetVtdvCustomColumnIndex(vm, customColumn.Name)}].Value"),
                    ElementStyle = (Style)FindResource("CvxdCellTextBlockWrapStyle"),
                    EditingElementStyle = (Style)FindResource("CvxdCellEditingWrapStyle"),
                    CanUserSort = false
                };
                column.SetValue(FrameworkElement.TagProperty, "vtdv-custom:" + customColumn.Name);

                DataGridVtdvItemsEmbedded.Columns.Add(column);
            }
        }

        private static StackPanel CreateColumnHeader(string name, ICommand renameCommand, ICommand deleteCommand)
        {
            var menu = CreateColumnContextMenu(name, renameCommand, deleteCommand);
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                ContextMenu = menu
            };

            panel.Children.Add(new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeBrush("Blueprint.OnSurface", "#FFFFFF")
            });

            panel.Children.Add(CreateHeaderMenuButton(menu));
            return panel;
        }

        private static ContextMenu CreateColumnContextMenu(string columnName, ICommand renameCommand, ICommand deleteCommand)
        {
            var menu = new ContextMenu
            {
                Background = ThemeBrush("Blueprint.SurfaceContainer", "#0F172A"),
                BorderBrush = ThemeBrush("Blueprint.Outline", "#334155"),
                BorderThickness = new Thickness(1)
            };
            menu.Items.Add(CreateColumnMenuItem("Đổi tên cột", columnName, renameCommand, "Blueprint.OnSurface", "#E2E8F0"));
            menu.Items.Add(CreateColumnMenuItem("Xóa cột", columnName, deleteCommand, "Blueprint.Error", "#FCA5A5"));
            return menu;
        }

        private static MenuItem CreateColumnMenuItem(string header, string columnName, ICommand command, string foregroundResourceKey, string fallback)
        {
            return new MenuItem
            {
                Header = header,
                Command = command,
                CommandParameter = columnName,
                Foreground = ThemeBrush(foregroundResourceKey, fallback),
                Background = Brushes.Transparent,
                Padding = new Thickness(14, 8, 18, 8)
            };
        }

        private static Button CreateHeaderMenuButton(ContextMenu menu)
        {
            var button = new Button
            {
                Content = "⋮",
                Height = 24,
                Width = 24,
                Padding = new Thickness(0),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeBrush("Blueprint.OnSurface", "#FFFFFF"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = "Tùy chọn cột"
            };
            button.Click += (_, e) =>
            {
                menu.PlacementTarget = button;
                menu.IsOpen = true;
                e.Handled = true;
            };
            return button;
        }

        private static Brush ThemeBrush(string resourceKey, string fallback)
        {
            return Application.Current?.TryFindResource(resourceKey) as Brush
                ?? (Brush)new BrushConverter().ConvertFromString(fallback)!;
        }

        private static int GetVtdvCustomColumnIndex(CvxdWindowViewModel vm, string name)
        {
            for (int i = 0; i < vm.CvxdInfo.VtdvCustomColumns.Count; i++)
            {
                if (string.Equals(vm.CvxdInfo.VtdvCustomColumns[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridCell cell || e.ClickCount >= 2) return;
            if (FindParent<Button>(e.OriginalSource as DependencyObject ?? cell) != null ||
                FindParent<CheckBox>(e.OriginalSource as DependencyObject ?? cell) != null ||
                FindParent<TextBox>(e.OriginalSource as DependencyObject ?? cell) != null ||
                FindParent<ComboBox>(e.OriginalSource as DependencyObject ?? cell) != null ||
                FindParent<ComboBoxItem>(e.OriginalSource as DependencyObject ?? cell) != null)
            {
                return;
            }

            var dg = FindParent<DataGrid>(cell);
            var item = cell.DataContext;
            if (dg == null || item == null || item == CollectionView.NewItemPlaceholder) return;
            if (dg.Name == "DataGridVtdvItemsEmbedded") ClearVtdvPasteColumn();

            var cellInfo = new DataGridCellInfo(item, cell.Column);
            if (!cellInfo.IsValid) return;

            // A click on the editing cell's background closes the current dropdown.
            // Do not enter edit mode again from that same click.
            if (IsCvxdComboBoxColumn(cell.Column) &&
                Keyboard.Modifiers == ModifierKeys.None &&
                cell.IsEditing)
            {
                e.Handled = true;
                CancelActiveCellEdit(dg);
                return;
            }

            e.Handled = true;
            dg.CommitEdit(DataGridEditingUnit.Cell, true);
            dg.CommitEdit(DataGridEditingUnit.Row, true);
            dg.Focus();
            dg.CurrentCell = cellInfo;
            if (dg.Name != "DataGridVtdvItemsEmbedded") dg.SelectedItem = item;
            else if (DataContext is CvxdWindowViewModel vtdvOwner && item is VtdvItem vtdvItem)
                vtdvOwner.VtdvViewModel!.SelectedVtdvItem = vtdvItem;

            if (IsCvxdComboBoxColumn(cell.Column) && Keyboard.Modifiers == ModifierKeys.None)
            {
                dg.SelectedCells.Clear();
                AddSelectedCell(dg, cellInfo);
                _selectionAnchors[dg] = cellInfo;

                if (dg.BeginEdit())
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var comboBox = FindVisualChild<ComboBox>(cell);
                        if (comboBox == null) return;

                        comboBox.Focus();
                        comboBox.IsDropDownOpen = true;
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }

                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift &&
                _selectionAnchors.TryGetValue(dg, out var anchor) && anchor.IsValid)
            {
                SelectCellRange(dg, anchor, cellInfo);
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (dg.SelectedCells.Contains(cellInfo)) dg.SelectedCells.Remove(cellInfo);
                else AddSelectedCell(dg, cellInfo);
                _selectionAnchors[dg] = cellInfo;
                return;
            }

            dg.SelectedCells.Clear();
            AddSelectedCell(dg, cellInfo);
            _selectionAnchors[dg] = cellInfo;
            _dragSelectionAnchors[dg] = cellInfo;
            cell.CaptureMouse();
        }

        private void CvxdComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is not ComboBox comboBox) return;
            var dataGrid = FindParent<DataGrid>(comboBox);
            var cell = FindParent<DataGridCell>(comboBox);
            if (dataGrid == null || cell?.DataContext == null) return;

            var cellInfo = new DataGridCellInfo(cell.DataContext, cell.Column);
            if (!cellInfo.IsValid) return;

            _comboBoxEditStates[comboBox] = new ComboBoxEditState(cellInfo, GetComboBoxValue(comboBox));
        }

        private void CvxdComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (sender is not ComboBox comboBox ||
                !_comboBoxEditStates.Remove(comboBox, out var editState)) return;

            var dataGrid = FindParent<DataGrid>(comboBox);
            if (dataGrid == null) return;

            var hasChanged = !Equals(editState.OriginalValue, GetComboBoxValue(comboBox));
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var currentCell = dataGrid.CurrentCell;
                if (!currentCell.IsValid ||
                    !ReferenceEquals(currentCell.Item, editState.Cell.Item) ||
                    !ReferenceEquals(currentCell.Column, editState.Cell.Column))
                {
                    return;
                }

                if (hasChanged)
                {
                    dataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    dataGrid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                else
                {
                    CancelActiveCellEdit(dataGrid);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private static object? GetComboBoxValue(ComboBox comboBox)
        {
            return comboBox.SelectedValue ?? comboBox.SelectedItem;
        }

        private static bool IsCvxdComboBoxColumn(DataGridColumn column)
        {
            return column.Header?.ToString() is "Loại NT" or "Loại BB" or "TCNT";
        }

        private void DataGridCell_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not DataGridCell sourceCell || e.LeftButton != MouseButtonState.Pressed) return;
            var dg = FindParent<DataGrid>(sourceCell);
            if (dg == null || !_dragSelectionAnchors.TryGetValue(dg, out var anchor)) return;

            var hit = (VisualTreeHelper.HitTest(dg, e.GetPosition(dg)) as PointHitTestResult)?.VisualHit;
            var targetCell = hit as DataGridCell ?? (hit == null ? null : FindParent<DataGridCell>(hit));
            if (targetCell?.DataContext == null || targetCell.DataContext == CollectionView.NewItemPlaceholder) return;

            var target = new DataGridCellInfo(targetCell.DataContext, targetCell.Column);
            if (target.IsValid) SelectCellRange(dg, anchor, target);
        }

        private void DataGridCell_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridCell cell) return;
            var dg = FindParent<DataGrid>(cell);
            if (dg != null) _dragSelectionAnchors.Remove(dg);
            if (cell.IsMouseCaptured) cell.ReleaseMouseCapture();
        }

        private void DataGridColumnHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridColumnHeader header || header.Column == null) return;
            // The first column contains the Select All checkbox. Let the
            // CheckBox receive the click so its TwoWay binding can update
            // CvxdWindowViewModel.AllSelected.
            if (e.OriginalSource is DependencyObject original && FindParent<CheckBox>(original) != null)
                return;
            // Let WPF's header-resize thumb receive the drag at the column edge.
            if (e.OriginalSource is DependencyObject source && FindParent<Thumb>(source) != null) return;
            var dg = FindParent<DataGrid>(header);
            if (dg == null) return;

            e.Handled = true;
            dg.Focus();
            if (dg.Name == "DataGridVtdvItemsEmbedded")
            {
                ClearVtdvPasteColumn();
                if (!header.Column.IsReadOnly)
                {
                    _vtdvPasteColumn = header.Column;
                    _vtdvPasteHeader = header;
                    header.SetResourceReference(Control.BackgroundProperty, "Blueprint.Primary");
                    header.SetResourceReference(Control.ForegroundProperty, "Blueprint.OnPrimary");
                }
            }
            var items = dg.Items.Cast<object>().Where(item => item != CollectionView.NewItemPlaceholder).ToList();
            dg.SelectedCells.Clear();
            foreach (var item in items) AddSelectedCell(dg, new DataGridCellInfo(item, header.Column));

            if (items.Count > 0)
            {
                var anchor = new DataGridCellInfo(items[0], header.Column);
                dg.CurrentCell = anchor;
                _selectionAnchors[dg] = anchor;
            }
        }

        private void DataGridRowHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRowHeader header) return;
            var dg = FindParent<DataGrid>(header);
            var item = header.DataContext;
            if (dg == null || item == null) return;
            if (dg.Name == "DataGridVtdvItemsEmbedded") ClearVtdvPasteColumn();

            e.Handled = true;
            dg.SelectedCells.Clear();
            foreach (var column in dg.Columns.Where(column => column.Visibility == Visibility.Visible))
                AddSelectedCell(dg, new DataGridCellInfo(item, column));

            var firstColumn = dg.Columns.FirstOrDefault(column => column.Visibility == Visibility.Visible);
            if (firstColumn != null)
            {
                var anchor = new DataGridCellInfo(item, firstColumn);
                dg.CurrentCell = anchor;
                _selectionAnchors[dg] = anchor;
            }
        }

        private static void SelectCellRange(DataGrid dg, DataGridCellInfo start, DataGridCellInfo end)
        {
            var items = dg.Items.Cast<object>().Where(item => item != CollectionView.NewItemPlaceholder).ToList();
            var columns = dg.Columns.Where(column => column.Visibility == Visibility.Visible).OrderBy(column => column.DisplayIndex).ToList();
            var startRow = items.IndexOf(start.Item);
            var endRow = items.IndexOf(end.Item);
            var startColumn = columns.IndexOf(start.Column);
            var endColumn = columns.IndexOf(end.Column);
            if (startRow < 0 || endRow < 0 || startColumn < 0 || endColumn < 0) return;

            dg.SelectedCells.Clear();
            for (var row = Math.Min(startRow, endRow); row <= Math.Max(startRow, endRow); row++)
            for (var column = Math.Min(startColumn, endColumn); column <= Math.Max(startColumn, endColumn); column++)
                AddSelectedCell(dg, new DataGridCellInfo(items[row], columns[column]));
        }

        private static void AddSelectedCell(DataGrid dg, DataGridCellInfo cell)
        {
            if (cell.IsValid && !dg.SelectedCells.Contains(cell))
            {
                dg.SelectedCells.Add(cell);
            }
        }

        private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not DataGrid dg || dg.IsReadOnly) return;

            if (e.Key == Key.Escape && IsEditingInput(e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
                CancelActiveCellEdit(dg);
                return;
            }

            if (e.Key == Key.Z &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                e.OriginalSource is DependencyObject undoSource &&
                FindParent<TextBox>(undoSource) is TextBox textBox)
            {
                if (textBox.CanUndo)
                {
                    textBox.Undo();
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab &&
                Keyboard.Modifiers == ModifierKeys.None &&
                TryHandleTabFromLastCell(dg))
            {
                e.Handled = true;
                return;
            }

            // Handle Paste (Ctrl+V)
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // Let the active editor handle paste; refreshing a DataGrid during edit is invalid.
                if (IsEditingInput(e.OriginalSource as DependencyObject)) return;

                e.Handled = true;
                PasteToDataGrid(dg);
            }
            // Handle Delete
            else if (e.Key == Key.Delete)
            {
                if (IsEditingInput(e.OriginalSource as DependencyObject))
                {
                    return;
                }

                if (dg.SelectedCells.Any())
                {
                    e.Handled = true;
                    ClearSelectedCells(dg);
                }
            }
        }

        private bool TryHandleTabFromLastCell(DataGrid dg)
        {
            if (DataContext is not CvxdWindowViewModel vm || !vm.IsAdmin) return false;

            var currentCell = dg.CurrentCell;
            if (!currentCell.IsValid || currentCell.Item is not CvxdItem currentItem) return false;

            var items = dg.Items.OfType<CvxdItem>().ToList();
            var currentRowIndex = items.IndexOf(currentItem);
            if (currentRowIndex != items.Count - 1) return false;

            var editableColumns = GetEditableNavigationColumns(dg);
            if (editableColumns.Count == 0) return false;

            var currentColumnIndex = editableColumns.IndexOf(currentCell.Column);
            if (currentColumnIndex != editableColumns.Count - 1) return false;
            if (!vm.AddRowCommand.CanExecute(null)) return false;

            dg.CommitEdit(DataGridEditingUnit.Cell, true);
            dg.CommitEdit(DataGridEditingUnit.Row, true);

            vm.SelectedCvxdItem = currentItem;
            var previousCount = vm.CvxdItems.Count;
            vm.AddRowCommand.Execute(null);
            if (vm.CvxdItems.Count <= previousCount) return true;

            var newItem = vm.CvxdItems.ElementAtOrDefault(currentRowIndex + 1) ?? vm.CvxdItems.LastOrDefault();
            if (newItem == null) return true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var firstEditableColumn = GetEditableNavigationColumns(dg).FirstOrDefault();
                if (firstEditableColumn == null) return;

                dg.SelectedItem = newItem;
                dg.CurrentCell = new DataGridCellInfo(newItem, firstEditableColumn);
                dg.ScrollIntoView(newItem, firstEditableColumn);
                dg.BeginEdit();
            }), System.Windows.Threading.DispatcherPriority.Background);

            return true;
        }

        private static List<DataGridColumn> GetEditableNavigationColumns(DataGrid dg)
        {
            return dg.Columns
                .Where(c => c.Visibility == Visibility.Visible && !c.IsReadOnly && c is not DataGridCheckBoxColumn)
                .OrderBy(c => c.DisplayIndex)
                .ToList();
        }

        private static bool IsEditingInput(DependencyObject? source)
        {
            if (source == null) return false;
            return FindParent<TextBox>(source) != null ||
                   FindParent<ComboBox>(source) != null ||
                   source is TextBox ||
                   source is ComboBox;
        }

        private static void ClearSelectedCells(DataGrid dg)
        {
            foreach (var cellInfo in dg.SelectedCells)
            {
                var item = cellInfo.Item;
                var column = cellInfo.Column;

                string? propertyName = GetPropertyNameFromColumn(column);
                if (GetColumnTag(column) is string tag && tag.StartsWith("custom:", StringComparison.Ordinal) && item is CvxdItem cvxdItem)
                {
                    SetCustomFieldValue(cvxdItem, tag["custom:".Length..], string.Empty);
                }
                else if (propertyName != null)
                {
                    PropertyInfo? prop = item.GetType().GetProperty(propertyName);
                    if (prop != null && prop.CanWrite && !column.IsReadOnly)
                    {
                        prop.SetValue(item, prop.PropertyType == typeof(string) ? string.Empty : null);
                    }
                }
            }
        }

        private void PasteToDataGrid(DataGrid dg)
        {
            string clipboardText = Clipboard.GetText();
            if (string.IsNullOrEmpty(clipboardText)) return;

            string[] lines = clipboardText.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            if (lines.Length == 0) return;

            var selectedTargets = GetSelectedEditableCells(dg);
            var undoActions = new List<IUndoableAction>();
            if (dg.Name == "DataGridVtdvItemsEmbedded" && _vtdvPasteColumn is { IsReadOnly: false } targetColumn)
            {
                var columnValues = lines.Reverse().SkipWhile(string.IsNullOrEmpty).Reverse()
                    .Select(line => line.Split('\t')[0]).ToList();
                var items = dg.Items.Cast<object>().Where(item => item != CollectionView.NewItemPlaceholder).ToList();
                for (var index = 0; index < Math.Min(items.Count, columnValues.Count); index++)
                {
                    if (SetPastedCellValue(items[index], targetColumn, columnValues[index].Trim()) is { } action)
                        undoActions.Add(action);
                }
                RecordPasteUndo(undoActions);
                return;
            }
            if (selectedTargets.Count > 1)
            {
                // A selected column receives one Excel value per row, without spilling
                // into the neighboring material columns.
                if (dg.Name == "DataGridVtdvItemsEmbedded" &&
                    selectedTargets.All(target => ReferenceEquals(target.Column, selectedTargets[0].Column)))
                {
                    var columnValues = lines.Reverse().SkipWhile(string.IsNullOrEmpty).Reverse()
                        .Select(line => line.Split('\t')[0]).ToList();
                    for (var index = 0; index < Math.Min(selectedTargets.Count, columnValues.Count); index++)
                    {
                        if (SetPastedCellValue(selectedTargets[index].Item, selectedTargets[index].Column,
                                columnValues[index].Trim()) is { } action)
                            undoActions.Add(action);
                    }
                    RecordPasteUndo(undoActions);
                    return;
                }

                var values = lines
                    .Reverse()
                    .SkipWhile(string.IsNullOrEmpty)
                    .Reverse()
                    .SelectMany(line => line.Split('\t'))
                    .ToList();
                if (values.Count == 0) return;
                for (var index = 0; index < selectedTargets.Count; index++)
                {
                    var value = values.Count == 1 ? values[0] : values.ElementAtOrDefault(index);
                    if (value == null) break;
                    if (SetPastedCellValue(selectedTargets[index].Item, selectedTargets[index].Column, value.Trim()) is { } action)
                        undoActions.Add(action);
                }
                RecordPasteUndo(undoActions);
                return;
            }

            var currentCell = dg.CurrentCell;
            if (!currentCell.IsValid) return;

            int startRowIndex = dg.Items.IndexOf(currentCell.Item);
            var visibleCols = dg.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToList();
            int startColIndex = visibleCols.IndexOf(currentCell.Column);

            if (startRowIndex < 0 || startColIndex < 0) return;

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                int rowIndex = startRowIndex + i;
                if (rowIndex >= dg.Items.Count) break;

                var item = dg.Items[rowIndex];
                string[] cells = lines[i].Split('\t');

                for (int j = 0; j < cells.Length; j++)
                {
                    int colIndex = startColIndex + j;
                    var column = visibleCols.ElementAtOrDefault(colIndex);

                    if (column == null || column.IsReadOnly) continue;

                    if (SetPastedCellValue(item, column, cells[j].Trim()) is { } action)
                        undoActions.Add(action);
                }
            }

            RecordPasteUndo(undoActions);
        }

        private void RecordPasteUndo(IEnumerable<IUndoableAction> actions)
        {
            if (DataContext is CvxdWindowViewModel vm)
                vm.RecordCellEditBatchUndo(actions);
        }

        private static List<DataGridCellInfo> GetSelectedEditableCells(DataGrid dg)
        {
            var items = dg.Items.Cast<object>().ToList();
            var columns = dg.Columns.Where(column => column.Visibility == Visibility.Visible).OrderBy(column => column.DisplayIndex).ToList();
            return dg.SelectedCells
                .Where(cell => cell.Item != CollectionView.NewItemPlaceholder &&
                               !cell.Column.IsReadOnly &&
                               (GetPropertyNameFromColumn(cell.Column) != null || GetCustomFieldNameFromColumn(cell.Column) != null))
                .OrderBy(cell => items.IndexOf(cell.Item))
                .ThenBy(cell => columns.IndexOf(cell.Column))
                .ToList();
        }

        private static IUndoableAction? SetPastedCellValue(object item, DataGridColumn column, string value)
        {
            if (GetColumnTag(column) is string tag && tag.StartsWith("custom:", StringComparison.Ordinal) && item is CvxdItem cvxdItem)
            {
                var customOldValue = GetCustomFieldValue(cvxdItem, tag["custom:".Length..]);
                SetCustomFieldValue(cvxdItem, tag["custom:".Length..], value);
                var field = GetCustomField(cvxdItem, tag["custom:".Length..]);
                return field == null || Equals(customOldValue, value)
                    ? null
                    : new PropertyChangeAction(field, nameof(CvxdCustomFieldValue.Value), customOldValue, value, _ => { });
            }

            var propertyName = GetPropertyNameFromColumn(column);
            var property = propertyName == null ? null : item.GetType().GetProperty(propertyName);
            if (property?.CanWrite != true) return null;

            if (!TryConvertPastedValue(value, property.PropertyType, out var pastedValue)) return null;

            var propertyOldValue = property.GetValue(item);
            if (Equals(propertyOldValue, pastedValue)) return null;
            property.SetValue(item, pastedValue);
            return new PropertyChangeAction(item, propertyName!, propertyOldValue, pastedValue, _ => { });
        }

        private static bool TryConvertPastedValue(string value, Type propertyType, out object? pastedValue)
        {
            var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (targetType == typeof(string))
            {
                pastedValue = value;
                return true;
            }

            if (targetType == typeof(bool))
            {
                var normalizedValue = value.Trim().ToLowerInvariant();
                if (normalizedValue is "true" or "1" or "yes" or "y" or "x" or "co" or "có" or "✓")
                {
                    pastedValue = true;
                    return true;
                }

                if (normalizedValue is "false" or "0" or "no" or "n" or "khong" or "không" or "✕")
                {
                    pastedValue = false;
                    return true;
                }

                pastedValue = null;
                return false;
            }

            try
            {
                pastedValue = Convert.ChangeType(value, targetType, CultureInfo.CurrentCulture);
                return true;
            }
            catch (FormatException)
            {
                pastedValue = null;
                return false;
            }
            catch (InvalidCastException)
            {
                pastedValue = null;
                return false;
            }
            catch (OverflowException)
            {
                pastedValue = null;
                return false;
            }
        }

        private static string? GetPropertyNameFromColumn(DataGridColumn column)
        {
            if (column is DataGridBoundColumn boundColumn)
            {
                if (GetCustomFieldNameFromColumn(column) != null) return null;
                if (boundColumn.Binding is Binding binding)
                    return binding.Path.Path;
            }
            else if (column is DataGridTemplateColumn)
            {
                string header = column.Header?.ToString() ?? "";
                if (header.Contains("Loại NT")) return "LoaiNt";
                if (header.Contains("Loại BB")) return "LoaiBb";
                if (header.Contains("TCNT")) return "Tcnt";
            }
            return null;
        }

        private static string? GetCustomFieldNameFromColumn(DataGridColumn column)
        {
            if (GetColumnTag(column) is not string tag) return null;
            if (tag.StartsWith("custom:", StringComparison.Ordinal)) return tag["custom:".Length..];
            if (tag.StartsWith("vtdv-custom:", StringComparison.Ordinal)) return tag["vtdv-custom:".Length..];
            return null;
        }

        private static string GetCustomFieldValue(object item, string name)
        {
            return GetCustomField(item, name)?.Value ?? string.Empty;
        }

        private static CvxdCustomFieldValue? GetCustomField(object item, string name)
        {
            var fields = item switch
            {
                CvxdItem cvxdItem => cvxdItem.CustomFields,
                VtdvItem vtdvItem => vtdvItem.CustomFields,
                _ => null
            };
            return fields?.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static void SetCustomFieldValue(object item, string name, string value)
        {
            ObservableCollection<CvxdCustomFieldValue>? fields = item switch
            {
                CvxdItem cvxdItem => cvxdItem.CustomFields,
                VtdvItem vtdvItem => vtdvItem.CustomFields,
                _ => null
            };
            if (fields == null) return;

            var field = fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (field == null)
            {
                field = new CvxdCustomFieldValue { Name = name };
                fields.Add(field);
            }

            field.Value = value;
        }

        private static object? GetColumnTag(DataGridColumn column)
        {
            return column.GetValue(FrameworkElement.TagProperty);
        }

        private void DataGrid_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (sender is DataGrid)
            {
                CaptureActiveCellEdit(e.Row.Item, e.Column);
            }

            // Find the TextBox inside the editing cell and constrain its width
            // so that TextWrapping actually works inside horizontally-scrollable DataGrids
            if (e.EditingElement is TextBox tb)
            {
                if (e.Column.ActualWidth > 0)
                {
                    tb.MaxWidth = e.Column.ActualWidth;
                }
            }
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
            {
                _activeEditSnapshot = null;
                return;
            }

            var snapshot = _activeEditSnapshot;
            if (snapshot == null || !ReferenceEquals(snapshot.Item, e.Row.Item) || !ReferenceEquals(snapshot.Column, e.Column))
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    RecordCommittedCellEdit(snapshot);
                }
                finally
                {
                    if (ReferenceEquals(_activeEditSnapshot, snapshot))
                        _activeEditSnapshot = null;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void CaptureActiveCellEdit(object item, DataGridColumn column)
        {
            string? propertyName = GetPropertyNameFromColumn(column);
            string? customFieldName = GetCustomFieldNameFromColumn(column);
            object? originalValue = null;

            if (!string.IsNullOrWhiteSpace(customFieldName))
            {
                originalValue = GetCustomFieldValue(item, customFieldName);
            }
            else if (!string.IsNullOrWhiteSpace(propertyName))
            {
                originalValue = item.GetType().GetProperty(propertyName)?.GetValue(item);
            }

            _activeEditSnapshot = new CellEditSnapshot(item, column, propertyName, customFieldName, originalValue);
        }

        private void CancelActiveCellEdit(DataGrid dg)
        {
            RestoreActiveEditSnapshot();
            dg.CancelEdit(DataGridEditingUnit.Cell);
            dg.CancelEdit(DataGridEditingUnit.Row);
            _activeEditSnapshot = null;
        }

        private void RestoreActiveEditSnapshot()
        {
            var snapshot = _activeEditSnapshot;
            if (snapshot == null) return;

            if (!string.IsNullOrWhiteSpace(snapshot.CustomFieldName))
            {
                SetCustomFieldValue(snapshot.Item, snapshot.CustomFieldName, snapshot.OriginalValue?.ToString() ?? string.Empty);
                return;
            }

            if (string.IsNullOrWhiteSpace(snapshot.PropertyName)) return;
            var prop = snapshot.Item.GetType().GetProperty(snapshot.PropertyName);
            if (prop == null || !prop.CanWrite) return;

            try
            {
                prop.SetValue(snapshot.Item, snapshot.OriginalValue);
            }
            catch
            {
                if (prop.PropertyType == typeof(string))
                    prop.SetValue(snapshot.Item, snapshot.OriginalValue?.ToString() ?? string.Empty);
            }
        }

        private void RecordCommittedCellEdit(CellEditSnapshot snapshot)
        {
            if (DataContext is not CvxdWindowViewModel vm) return;

            if (!string.IsNullOrWhiteSpace(snapshot.CustomFieldName))
            {
                var field = GetCustomField(snapshot.Item, snapshot.CustomFieldName);
                if (field == null) return;

                vm.RecordCellEditUndo(field, nameof(CvxdCustomFieldValue.Value), snapshot.OriginalValue, field.Value);
                return;
            }

            if (string.IsNullOrWhiteSpace(snapshot.PropertyName)) return;
            var prop = snapshot.Item.GetType().GetProperty(snapshot.PropertyName);
            if (prop == null || !prop.CanRead) return;

            vm.RecordCellEditUndo(snapshot.Item, snapshot.PropertyName, snapshot.OriginalValue, prop.GetValue(snapshot.Item));
        }

        private void DataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedCells.Count > 0)
            {
                if (DataContext is ViewModels.CvxdWindowViewModel vm)
                {
                    if (dg.SelectedCells[0].Item is CvxdItem item)
                    {
                        vm.SelectedCvxdItem = item;
                    }
                    else if (dg.SelectedCells[0].Item is VtdvItem vtdvItem &&
                             dg.Name == "DataGridVtdvItemsEmbedded" &&
                             vm.VtdvViewModel != null)
                    {
                        vm.VtdvViewModel.SelectedVtdvItem = vtdvItem;
                    }
                }
            }
        }

        private void DataGridVtdvItemsEmbedded_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not DataGrid dg) return;

            if (e.Key == Key.Escape && IsEditingInput(e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
                CancelActiveCellEdit(dg);
                return;
            }

            if (e.Key == Key.Z &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                e.OriginalSource is DependencyObject undoSource &&
                FindParent<TextBox>(undoSource) is TextBox textBox)
            {
                if (textBox.CanUndo)
                {
                    textBox.Undo();
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (IsEditingInput(e.OriginalSource as DependencyObject)) return;
                e.Handled = true;
                PasteToDataGrid(dg);
                return;
            }

            if (e.Key != Key.Tab || Keyboard.Modifiers != ModifierKeys.None) return;
            if (DataContext is not CvxdWindowViewModel vm || vm.VtdvViewModel == null) return;
            if (TryHandleTabFromLastVtdvCell(dg, vm.VtdvViewModel))
            {
                e.Handled = true;
            }
        }

        private bool TryHandleTabFromLastVtdvCell(DataGrid dg, VtdvViewModel vm)
        {
            if (vm.SelectedSession == null) return false;

            var currentCell = dg.CurrentCell;
            if (!currentCell.IsValid || currentCell.Item is not VtdvItem currentItem) return false;

            var items = vm.SelectedSession.Items;
            var currentRowIndex = items.IndexOf(currentItem);
            if (currentRowIndex != items.Count - 1) return false;

            var editableColumns = GetEditableVtdvNavigationColumns(dg);
            if (editableColumns.Count == 0) return false;

            var currentColumnIndex = editableColumns.IndexOf(currentCell.Column);
            if (currentColumnIndex != editableColumns.Count - 1) return false;
            if (!vm.AddItemCommand.CanExecute(null)) return false;

            dg.CommitEdit(DataGridEditingUnit.Cell, true);
            dg.CommitEdit(DataGridEditingUnit.Row, true);

            vm.SelectedVtdvItem = currentItem;
            var previousCount = items.Count;
            vm.AddItemCommand.Execute(null);
            if (items.Count <= previousCount || vm.SelectedVtdvItem == null) return true;

            var newItem = vm.SelectedVtdvItem;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var firstEditableColumn = GetEditableVtdvNavigationColumns(dg).FirstOrDefault();
                if (firstEditableColumn == null) return;

                dg.SelectedItem = newItem;
                dg.CurrentCell = new DataGridCellInfo(newItem, firstEditableColumn);
                dg.ScrollIntoView(newItem, firstEditableColumn);
                dg.BeginEdit();
            }), System.Windows.Threading.DispatcherPriority.Background);

            return true;
        }

        private static List<DataGridColumn> GetEditableVtdvNavigationColumns(DataGrid dg)
        {
            return dg.Columns
                .Where(c => c.Visibility == Visibility.Visible && !c.IsReadOnly && c is DataGridBoundColumn)
                .OrderBy(c => c.DisplayIndex)
                .ToList();
        }

        private void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Focus();
                tb.SelectAll();
            }
        }

        private void OtherTemplateDescription_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is CvxdWindowViewModel vm)
            {
                vm.SaveOtherTemplateDescriptions();
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && parent is not T)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match) return match;

                var descendant = FindVisualChild<T>(child);
                if (descendant != null) return descendant;
            }

            return null;
        }
    }
}
