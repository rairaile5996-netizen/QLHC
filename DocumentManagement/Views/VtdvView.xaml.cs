using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using DocumentManagement.Models;
using DocumentManagement.ViewModels;
using DocumentManagement.Utilities;

namespace DocumentManagement.Views
{
    public partial class VtdvView : System.Windows.Controls.UserControl
    {
        public VtdvView()
        {
            InitializeComponent();
            Loaded += VtdvView_Loaded;
            Loaded += (_, _) => AttachCustomColumnHandlers();
            DataContextChanged += (_, _) => AttachCustomColumnHandlers();
        }

        private void FindReplace_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is VtdvViewModel vm)
                new FindReplaceWindow(vm, (find, advance) => FocusFindMatch(vm, find, advance)) { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control ||
                (e.Key != Key.F && e.Key != Key.H)) return;
            if (DataContext is not VtdvViewModel vm) return;

            e.Handled = true;
            var window = new FindReplaceWindow(vm, (find, advance) => FocusFindMatch(vm, find, advance))
            { Owner = Window.GetWindow(this) };
            window.Show();
            if (e.Key == Key.H) window.FocusReplaceInput();
        }

        private void FocusFindMatch(VtdvViewModel vm, string find, bool advance)
        {
            var item = vm.FindNextItem(find, advance);
            if (item == null) return;
            vm.SelectedVtdvItem = item;
            DataGridVtdvItems.ScrollIntoView(item);
        }

        private VtdvViewModel? _attachedVm;
        private CellEditSnapshot? _activeEditSnapshot;
        private readonly Dictionary<DataGrid, DataGridCellInfo> _selectionAnchors = new();
        private readonly Dictionary<DataGrid, DataGridCellInfo> _dragSelectionAnchors = new();
        private DataGridColumn? _pasteColumn;
        private DataGridColumnHeader? _pasteHeader;

        private void ClearPasteColumn()
        {
            _pasteHeader?.ClearValue(Control.BackgroundProperty);
            _pasteHeader?.ClearValue(Control.ForegroundProperty);
            _pasteHeader = null;
            _pasteColumn = null;
        }

        private sealed record CellEditSnapshot(object Item, DataGridColumn Column, string? PropertyName, string? CustomFieldName, object? OriginalValue);

        private void VtdvView_Loaded(object sender, RoutedEventArgs e)
        {
            // Khi nhúng trong CvxdView, Tag sẽ là "Embedded" → ẩn sidebar riêng
            if (Tag is string tag && tag == "Embedded")
            {
                SidebarBorder.Visibility = Visibility.Collapsed;
                SidebarColumn.Width = new GridLength(0);
            }
            RefreshCustomColumns();
        }

        private bool? _compactSidebarBand;
        private bool? _compactContentBand;

        private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // The actions' own Grid can be measured wider than the visible viewport.
            // Use the root viewport instead so the heading and buttons never overlap.
            var compactMaterialActions = e.NewSize.Width < 1400;
            Grid.SetRow(VtdvMaterialActions, compactMaterialActions ? 1 : 0);
            VtdvMaterialActions.Margin = compactMaterialActions ? new Thickness(0, 8, 0, 0) : new Thickness(0);

            var compact = e.NewSize.Width < 1180;
            if (_compactSidebarBand != compact)
            {
                _compactSidebarBand = compact;
                SidebarCompactToggle.IsChecked = compact;
            }

            var compactContent = e.NewSize.Width < 980;
            if (_compactContentBand == compactContent) return;
            _compactContentBand = compactContent;
            Grid.SetColumnSpan(VtdvHeaderInputs, compactContent ? 2 : 1);
            Grid.SetRow(VtdvHeaderActions, compactContent ? 1 : 0);
            Grid.SetColumn(VtdvHeaderActions, compactContent ? 0 : 1);
            Grid.SetColumnSpan(VtdvHeaderActions, compactContent ? 2 : 1);
            VtdvHeaderActions.Margin = compactContent ? new Thickness(0, 12, 0, 0) : new Thickness(0);

            VtdvSessionListColumn.Width = compactContent ? new GridLength(1, GridUnitType.Star) : new GridLength(280);
            VtdvSessionDetailColumn.Width = compactContent ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            VtdvSessionList.Height = compactContent ? 260 : double.NaN;
            Grid.SetColumn(VtdvSessionDetail, compactContent ? 0 : 1);
            Grid.SetRow(VtdvSessionDetail, compactContent ? 1 : 0);
        }

        private void AttachCustomColumnHandlers()
        {
            if (_attachedVm != null)
            {
                _attachedVm.PropertyChanged -= ViewModel_PropertyChanged;
                _attachedVm.ProjectInfo.VtdvCustomColumns.CollectionChanged -= CustomColumns_CollectionChanged;
            }

            _attachedVm = DataContext as VtdvViewModel;
            if (_attachedVm != null)
            {
                _attachedVm.PropertyChanged += ViewModel_PropertyChanged;
                _attachedVm.ProjectInfo.VtdvCustomColumns.CollectionChanged += CustomColumns_CollectionChanged;
            }

            RefreshCustomColumns();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VtdvViewModel.SelectedSession) ||
                e.PropertyName == nameof(VtdvViewModel.ProjectInfo))
            {
                ClearPasteColumn();
                RefreshCustomColumns();
            }
        }

        private void CustomColumns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshCustomColumns();
        }

        private void RefreshCustomColumns()
        {
            if (DataGridVtdvItems == null || DataContext is not VtdvViewModel vm) return;

            for (int i = DataGridVtdvItems.Columns.Count - 1; i >= 0; i--)
            {
                if (DataGridVtdvItems.Columns[i].GetValue(FrameworkElement.TagProperty) is string tag &&
                    tag.StartsWith("custom:", StringComparison.Ordinal))
                {
                    DataGridVtdvItems.Columns.RemoveAt(i);
                }
            }

            if (vm.SelectedSession != null)
            {
                foreach (var item in vm.SelectedSession.Items)
                {
                    var orderedFields = new ObservableCollection<CvxdCustomFieldValue>();
                    foreach (var customColumn in vm.ProjectInfo.VtdvCustomColumns)
                    {
                        var field = item.CustomFields.FirstOrDefault(f => string.Equals(f.Name, customColumn.Name, StringComparison.OrdinalIgnoreCase));
                        field ??= new CvxdCustomFieldValue { Name = customColumn.Name };
                        orderedFields.Add(field);
                    }
                    item.CustomFields = orderedFields;
                }
            }

            foreach (var customColumn in vm.ProjectInfo.VtdvCustomColumns.Where(c => !string.IsNullOrWhiteSpace(c.Name)))
            {
                var column = new DataGridTextColumn
                {
                    Header = CreateColumnHeader(customColumn.Name, vm.RenameVtdvCustomColumnCommand, vm.DeleteVtdvCustomColumnCommand),
                    Width = 150,
                    Binding = new Binding($"CustomFields[{GetCustomColumnIndex(vm, customColumn.Name)}].Value"),
                    ElementStyle = (Style)FindResource("VtdvCellTextBlockWrapStyle"),
                    EditingElementStyle = (Style)FindResource("VtdvCellEditingWrapStyle"),
                    CanUserSort = false
                };
                column.SetValue(FrameworkElement.TagProperty, "custom:" + customColumn.Name);
                DataGridVtdvItems.Columns.Add(column);
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

        private static int GetCustomColumnIndex(VtdvViewModel vm, string name)
        {
            for (int i = 0; i < vm.ProjectInfo.VtdvCustomColumns.Count; i++)
            {
                if (string.Equals(vm.ProjectInfo.VtdvCustomColumns[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        private void DataGrid_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
        {
            CaptureActiveCellEdit(e.Row.Item, e.Column);

            if (e.EditingElement is TextBox tb)
            {
                if (e.Column.ActualWidth > 0)
                {
                    tb.MaxWidth = e.Column.ActualWidth;
                }
            }
        }

        private void DataGrid_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
        {
            if (e.EditingEventArgs is MouseButtonEventArgs mouseArgs && mouseArgs.ClickCount < 2)
            {
                e.Cancel = true;
            }
        }

        private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridCell cell) return;
            if (e.ClickCount >= 2) return;
            if (FindParent<Button>(e.OriginalSource as DependencyObject ?? cell) != null ||
                FindParent<CheckBox>(e.OriginalSource as DependencyObject ?? cell) != null ||
                FindParent<TextBox>(e.OriginalSource as DependencyObject ?? cell) != null)
            {
                return;
            }

            var dg = FindParent<DataGrid>(cell);
            if (dg == null || dg.SelectionUnit == DataGridSelectionUnit.FullRow) return;
            if (dg == DataGridVtdvItems) ClearPasteColumn();

            var item = cell.DataContext;
            if (item == null || item == CollectionView.NewItemPlaceholder) return;

            var cellInfo = new DataGridCellInfo(item, cell.Column);
            if (!cellInfo.IsValid) return;

            e.Handled = true;
            dg.CommitEdit(DataGridEditingUnit.Cell, true);
            dg.CommitEdit(DataGridEditingUnit.Row, true);
            dg.Focus();
            cell.Focus();
            dg.CurrentCell = cellInfo;
            if (DataContext is VtdvViewModel vm && item is VtdvItem vtdvItem)
            {
                vm.SelectedVtdvItem = vtdvItem;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift &&
                _selectionAnchors.TryGetValue(dg, out var anchor) &&
                anchor.IsValid)
            {
                SelectCellRange(dg, anchor, cellInfo);
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (dg.SelectedCells.Contains(cellInfo))
                {
                    dg.SelectedCells.Remove(cellInfo);
                }
                else
                {
                    AddSelectedCell(dg, cellInfo);
                }

                _selectionAnchors[dg] = cellInfo;
                return;
            }

            dg.SelectedCells.Clear();
            AddSelectedCell(dg, cellInfo);
            _selectionAnchors[dg] = cellInfo;
            _dragSelectionAnchors[dg] = cellInfo;
            cell.CaptureMouse();
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
            // Let WPF's header-resize thumb receive the drag at the column edge.
            if (e.OriginalSource is DependencyObject source && FindParent<Thumb>(source) != null) return;
            var dg = FindParent<DataGrid>(header);
            if (dg == null) return;

            e.Handled = true;
            dg.Focus();
            if (dg == DataGridVtdvItems)
            {
                ClearPasteColumn();
                if (!header.Column.IsReadOnly)
                {
                    _pasteColumn = header.Column;
                    _pasteHeader = header;
                    header.SetResourceReference(Control.BackgroundProperty, "Blueprint.Primary");
                    header.SetResourceReference(Control.ForegroundProperty, "Blueprint.OnPrimary");
                }
            }
            dg.CommitEdit(DataGridEditingUnit.Cell, true);
            dg.CommitEdit(DataGridEditingUnit.Row, true);
            var items = dg.Items.Cast<object>().Where(item => item != CollectionView.NewItemPlaceholder).ToList();
            dg.SelectedCells.Clear();
            foreach (var item in items)
            {
                AddSelectedCell(dg, new DataGridCellInfo(item, header.Column));
            }

            if (items.Count > 0)
            {
                var anchor = new DataGridCellInfo(items[0], header.Column);
                dg.CurrentCell = anchor;
                _selectionAnchors[dg] = anchor;
            }
        }

        private void DataGridRowHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRowHeader header || header.DataContext is not object item) return;
            var dg = FindParent<DataGrid>(header);
            if (dg == null) return;
            if (dg == DataGridVtdvItems) ClearPasteColumn();

            e.Handled = true;
            dg.CommitEdit(DataGridEditingUnit.Cell, true);
            dg.CommitEdit(DataGridEditingUnit.Row, true);
            dg.SelectedCells.Clear();
            foreach (var column in dg.Columns.Where(column => column.Visibility == Visibility.Visible))
            {
                AddSelectedCell(dg, new DataGridCellInfo(item, column));
            }

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
            var items = dg.Items.Cast<object>().Where(i => i != CollectionView.NewItemPlaceholder).ToList();
            var columns = dg.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToList();
            var startRow = items.IndexOf(start.Item);
            var endRow = items.IndexOf(end.Item);
            var startColumn = columns.IndexOf(start.Column);
            var endColumn = columns.IndexOf(end.Column);
            if (startRow < 0 || endRow < 0 || startColumn < 0 || endColumn < 0) return;

            dg.SelectedCells.Clear();
            for (var row = Math.Min(startRow, endRow); row <= Math.Max(startRow, endRow); row++)
            {
                for (var column = Math.Min(startColumn, endColumn); column <= Math.Max(startColumn, endColumn); column++)
                {
                    AddSelectedCell(dg, new DataGridCellInfo(items[row], columns[column]));
                }
            }
        }

        private static void AddSelectedCell(DataGrid dg, DataGridCellInfo cell)
        {
            if (cell.IsValid && !dg.SelectedCells.Contains(cell))
            {
                dg.SelectedCells.Add(cell);
            }
        }

        private void DataGridVtdvItems_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not DataGrid dg) return;

            if (TryInsertLineBreakInVtdvNameCell(dg, e))
            {
                return;
            }

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
                if (IsEditingInput(e.OriginalSource as DependencyObject))
                {
                    return;
                }

                e.Handled = true;
                PasteToDataGrid(dg);
                return;
            }

            if (e.Key != Key.Tab || Keyboard.Modifiers != ModifierKeys.None) return;
            if (DataContext is not VtdvViewModel vm) return;
            if (TryHandleTabFromLastVtdvCell(dg, vm))
            {
                e.Handled = true;
            }
        }

        private static bool TryInsertLineBreakInVtdvNameCell(DataGrid dg, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.Alt)
                return false;

            if (dg.CurrentCell.Item is not VtdvItem || GetPropertyNameFromColumn(dg.CurrentCell.Column) != nameof(VtdvItem.DoiTuong))
                return false;

            var source = e.OriginalSource as DependencyObject;
            var textBox = source as TextBox ?? (source == null ? null : FindParent<TextBox>(source));
            if (textBox == null)
                return false;

            var lineBreak = Environment.NewLine;
            var selectionStart = textBox.SelectionStart;
            textBox.SelectedText = lineBreak;
            textBox.SelectionStart = selectionStart + lineBreak.Length;
            textBox.SelectionLength = 0;
            e.Handled = true;
            return true;
        }

        private void AddVtdvItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not VtdvViewModel vm || vm.SelectedSession == null) return;

            var anchorItem = GetCurrentVtdvItem(DataGridVtdvItems);
            if (!vm.AddItemCommand.CanExecute(anchorItem)) return;

            DataGridVtdvItems.CommitEdit(DataGridEditingUnit.Cell, true);
            DataGridVtdvItems.CommitEdit(DataGridEditingUnit.Row, true);
            vm.AddItemCommand.Execute(anchorItem);

            if (vm.SelectedVtdvItem != null)
            {
                DataGridVtdvItems.ScrollIntoView(vm.SelectedVtdvItem);
            }
        }

        private static VtdvItem? GetCurrentVtdvItem(DataGrid dg)
        {
            if (dg.CurrentCell.IsValid && dg.CurrentCell.Item is VtdvItem currentItem)
            {
                return currentItem;
            }

            if (dg.SelectedCells.FirstOrDefault().Item is VtdvItem selectedCellItem)
            {
                return selectedCellItem;
            }

            return dg.SelectedItem as VtdvItem;
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

        private void RecordCommittedCellEdit(CellEditSnapshot snapshot)
        {
            if (DataContext is not VtdvViewModel vm) return;

            if (!string.IsNullOrWhiteSpace(snapshot.CustomFieldName))
            {
                var field = GetCustomField(snapshot.Item, snapshot.CustomFieldName);
                if (field == null) return;

                vm.RecordVtdvCellEditUndo(field, nameof(CvxdCustomFieldValue.Value), snapshot.OriginalValue, field.Value);
                return;
            }

            if (string.IsNullOrWhiteSpace(snapshot.PropertyName)) return;
            var prop = snapshot.Item.GetType().GetProperty(snapshot.PropertyName);
            if (prop == null || !prop.CanRead) return;

            vm.RecordVtdvCellEditUndo(snapshot.Item, snapshot.PropertyName, snapshot.OriginalValue, prop.GetValue(snapshot.Item));
        }

        private bool TryHandleTabFromLastVtdvCell(DataGrid dg, VtdvViewModel vm)
        {
            if (vm.SelectedSession == null) return false;

            var currentCell = dg.CurrentCell;
            if (!currentCell.IsValid || currentCell.Item is not VtdvItem currentItem) return false;

            var items = vm.SelectedSession.Items;
            var currentRowIndex = items.IndexOf(currentItem);
            if (currentRowIndex != items.Count - 1) return false;

            var editableColumns = GetEditableNavigationColumns(dg);
            if (editableColumns.Count == 0) return false;

            var currentColumnIndex = editableColumns.IndexOf(currentCell.Column);
            if (currentColumnIndex != editableColumns.Count - 1) return false;
            if (!vm.AddItemCommand.CanExecute(currentItem)) return false;

            dg.CommitEdit(DataGridEditingUnit.Cell, true);
            dg.CommitEdit(DataGridEditingUnit.Row, true);

            vm.SelectedVtdvItem = currentItem;
            var previousCount = items.Count;
            vm.AddItemCommand.Execute(currentItem);
            if (items.Count <= previousCount || vm.SelectedVtdvItem == null) return true;

            var newItem = vm.SelectedVtdvItem;
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

        private void PasteToDataGrid(DataGrid dg)
        {
            string clipboardText = Clipboard.GetText();
            if (string.IsNullOrEmpty(clipboardText)) return;

            string[] lines = clipboardText.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            if (lines.Length == 0) return;

            var selectedTargets = GetSelectedEditableCells(dg);
            var undoActions = new List<IUndoableAction>();
            if (dg == DataGridVtdvItems && _pasteColumn is { IsReadOnly: false } targetColumn)
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
                dg.Items.Refresh();
                return;
            }
            if (selectedTargets.Count > 1)
            {
                // Pasting into a selected column uses one Excel value per row.
                if (selectedTargets.All(target => ReferenceEquals(target.Column, selectedTargets[0].Column)))
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
                    dg.Items.Refresh();
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
                dg.Items.Refresh();
                return;
            }

            if (!TryGetPasteStartCell(dg, out var startItem, out var startColumn)) return;

            int startRowIndex = dg.Items.IndexOf(startItem);
            var visibleColumns = dg.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToList();
            int startColumnIndex = visibleColumns.IndexOf(startColumn);

            if (startRowIndex < 0 || startColumnIndex < 0) return;

            dg.CommitEdit(DataGridEditingUnit.Cell, true);
            dg.CommitEdit(DataGridEditingUnit.Row, true);

            for (int rowOffset = 0; rowOffset < lines.Length; rowOffset++)
            {
                if (string.IsNullOrWhiteSpace(lines[rowOffset])) continue;

                int rowIndex = startRowIndex + rowOffset;
                if (rowIndex >= dg.Items.Count) break;

                var item = dg.Items[rowIndex];
                string[] cells = lines[rowOffset].Split('\t');

                for (int columnOffset = 0; columnOffset < cells.Length; columnOffset++)
                {
                    int columnIndex = startColumnIndex + columnOffset;
                    var column = visibleColumns.ElementAtOrDefault(columnIndex);
                    if (column == null || column.IsReadOnly) continue;

                    if (SetPastedCellValue(item, column, cells[columnOffset].Trim()) is { } action)
                        undoActions.Add(action);
                }
            }

            RecordPasteUndo(undoActions);
            dg.Items.Refresh();
        }

        private void RecordPasteUndo(IEnumerable<IUndoableAction> actions)
        {
            if (DataContext is VtdvViewModel vm)
                vm.RecordVtdvCellEditBatchUndo(actions);
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
            if (GetCustomFieldNameFromColumn(column) is string customFieldName)
            {
                var customOldValue = GetCustomFieldValue(item, customFieldName);
                SetCustomFieldValue(item, customFieldName, value);
                var field = GetCustomField(item, customFieldName);
                return field == null || Equals(customOldValue, value)
                    ? null
                    : new PropertyChangeAction(field, nameof(CvxdCustomFieldValue.Value), customOldValue, value, _ => { });
            }

            var propertyName = GetPropertyNameFromColumn(column);
            var property = propertyName == null ? null : item.GetType().GetProperty(propertyName);
            if (property?.CanWrite != true) return null;
            var propertyOldValue = property.GetValue(item);
            if (Equals(propertyOldValue, value)) return null;
            property.SetValue(item, value);
            return new PropertyChangeAction(item, propertyName!, propertyOldValue, value, _ => { });
        }

        private static bool TryGetPasteStartCell(DataGrid dg, out object item, out DataGridColumn column)
        {
            if (dg.SelectedCells.Count > 0)
            {
                var visibleItems = dg.Items.Cast<object>().ToList();
                var visibleColumns = dg.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToList();

                var firstCell = dg.SelectedCells
                    .Where(c => c.Item != CollectionView.NewItemPlaceholder && visibleColumns.Contains(c.Column))
                    .OrderBy(c => visibleItems.IndexOf(c.Item))
                    .ThenBy(c => visibleColumns.IndexOf(c.Column))
                    .FirstOrDefault();

                if (firstCell.IsValid)
                {
                    item = firstCell.Item;
                    column = firstCell.Column;
                    return true;
                }
            }

            var currentCell = dg.CurrentCell;
            if (currentCell.IsValid)
            {
                item = currentCell.Item;
                column = currentCell.Column;
                return true;
            }

            item = null!;
            column = null!;
            return false;
        }

        private static List<DataGridColumn> GetEditableNavigationColumns(DataGrid dg)
        {
            return dg.Columns
                .Where(c => c.Visibility == Visibility.Visible && !c.IsReadOnly && c is DataGridBoundColumn)
                .OrderBy(c => c.DisplayIndex)
                .ToList();
        }

        private static string? GetPropertyNameFromColumn(DataGridColumn column)
        {
            if (column is not DataGridBoundColumn boundColumn) return null;
            if (GetCustomFieldNameFromColumn(column) != null) return null;
            return boundColumn.Binding is Binding binding ? binding.Path.Path : null;
        }

        private static string? GetCustomFieldNameFromColumn(DataGridColumn column)
        {
            if (column.GetValue(FrameworkElement.TagProperty) is not string tag) return null;
            return tag.StartsWith("custom:", StringComparison.Ordinal) ? tag["custom:".Length..] : null;
        }

        private static string GetCustomFieldValue(object item, string name)
        {
            return GetCustomField(item, name)?.Value ?? string.Empty;
        }

        private static CvxdCustomFieldValue? GetCustomField(object item, string name)
        {
            return item is VtdvItem vtdvItem
                ? vtdvItem.CustomFields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                : null;
        }

        private static void SetCustomFieldValue(object item, string name, string value)
        {
            if (item is not VtdvItem vtdvItem) return;

            var field = vtdvItem.CustomFields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (field == null)
            {
                field = new CvxdCustomFieldValue { Name = name };
                vtdvItem.CustomFields.Add(field);
            }

            field.Value = value;
        }

        private static bool IsEditingInput(DependencyObject? source)
        {
            if (source == null) return false;
            return FindParent<TextBox>(source) != null ||
                   source is TextBox;
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

        private void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Focus();
                tb.SelectAll();
            }
        }
    }
}
