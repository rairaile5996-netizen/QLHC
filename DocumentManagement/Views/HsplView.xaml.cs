using System.Windows.Input;
using DocumentManagement.ViewModels;

namespace DocumentManagement.Views;

public partial class HsplView : System.Windows.Controls.UserControl
{
    public HsplView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdatePageSizes();
        DataContextChanged += (_, _) => UpdatePageSizes();
    }

    private bool? _compactLayout;
    private bool? _tabbedLayout;
    private bool _showWorkers;

    private void UpdatePageSizes()
    {
        UpdatePageSize(PersonnelGrid);
        UpdatePageSize(WorkersGrid);
    }

    private void UpdatePageSize(System.Windows.Controls.DataGrid grid)
    {
        if (DataContext is not HsplViewModel viewModel ||
            grid.ActualHeight < grid.ColumnHeaderHeight + grid.RowHeight)
            return;

        // Leave room for the header and border so the last row remains fully visible.
        var visibleRows = Math.Max(1, (int)Math.Floor(
            (grid.ActualHeight - grid.ColumnHeaderHeight - 4) / grid.RowHeight));
        if (ReferenceEquals(grid, PersonnelGrid))
            viewModel.SetPersonnelPageSize(visibleRows);
        else
            viewModel.SetWorkersPageSize(visibleRows);
    }

    private void ListGrid_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGrid grid)
            UpdatePageSize(grid);
    }

    private void HsplView_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        var tabbedLayout = e.NewSize.Width < 1500 || e.NewSize.Height < 700;
        if (_tabbedLayout != tabbedLayout)
        {
            _tabbedLayout = tabbedLayout;
            CompactListTabs.Visibility = tabbedLayout ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            MainColumn.ColumnDefinitions[1].Width = new System.Windows.GridLength(tabbedLayout ? 0 : 16);
            MainColumn.ColumnDefinitions[2].Width = new System.Windows.GridLength(tabbedLayout ? 0 : 1,
                tabbedLayout ? System.Windows.GridUnitType.Pixel : System.Windows.GridUnitType.Star);
            System.Windows.Controls.Grid.SetColumn(WorkersPanel, tabbedLayout ? 0 : 2);
            UpdateListVisibility();
        }

        var compact = e.NewSize.Width < 850;
        if (_compactLayout == compact)
            return;

        _compactLayout = compact;
        ContentLayout.ColumnDefinitions[0].Width = compact
            ? new System.Windows.GridLength(1, System.Windows.GridUnitType.Star)
            : new System.Windows.GridLength(3, System.Windows.GridUnitType.Star);
        ContentLayout.ColumnDefinitions[1].Width = compact
            ? new System.Windows.GridLength(0)
            : new System.Windows.GridLength(16);
        ContentLayout.ColumnDefinitions[2].Width = compact
            ? new System.Windows.GridLength(0)
            : new System.Windows.GridLength(300);

        System.Windows.Controls.Grid.SetRow(CreatePanel, compact ? 2 : 0);
        System.Windows.Controls.Grid.SetColumn(CreatePanel, compact ? 0 : 2);
    }

    private void PersonnelTab_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _showWorkers = false;
        UpdateListVisibility();
    }

    private void WorkersTab_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _showWorkers = true;
        UpdateListVisibility();
    }

    private void UpdateListVisibility()
    {
        if (_tabbedLayout != true)
        {
            PersonnelPanel.Visibility = System.Windows.Visibility.Visible;
            WorkersPanel.Visibility = System.Windows.Visibility.Visible;
            return;
        }

        PersonnelPanel.Visibility = _showWorkers ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        WorkersPanel.Visibility = _showWorkers ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        PersonnelTabButton.Style = (System.Windows.Style)FindResource(_showWorkers ? "SecondaryButtonStyle" : "PrimaryButtonStyle");
        WorkersTabButton.Style = (System.Windows.Style)FindResource(_showWorkers ? "PrimaryButtonStyle" : "SecondaryButtonStyle");
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.S || Keyboard.Modifiers != ModifierKeys.Control || DataContext is not HsplViewModel viewModel)
            return;

        PersonnelGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        PersonnelGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        WorkersGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        WorkersGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        viewModel.SaveDataCommand.Execute(null);
        e.Handled = true;
    }
}
