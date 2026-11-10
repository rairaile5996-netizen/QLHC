using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using DocumentManagement.Models;
using DocumentManagement.ViewModels;

namespace DocumentManagement.Views;

public partial class SharedProjectInfoTestView : UserControl
{
    public SharedProjectInfoTestView() => InitializeComponent();

    private void ProjectInfoScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        FindParent<ScrollViewer>(ProjectInfoScrollViewer)?.ScrollToTop();
    }

    private void ProjectInfoScrollViewer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;
        FindParent<ScrollViewer>(ProjectInfoScrollViewer)?.ScrollToTop();
    }

    private void InfoCards_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid layout) return;
        var compact = e.NewSize.Width < 1100;
        if (layout.Tag is bool previous && previous == compact) return;

        Border? project = null;
        Border? contract = null;
        foreach (UIElement child in layout.Children)
        {
            if (child is Border { Name: "ProjectInfoCard" } projectCard) project = projectCard;
            if (child is Border { Name: "ContractInfoCard" } contractCard) contract = contractCard;
        }
        if (project == null || contract == null) return;

        layout.Tag = compact;
        Grid.SetColumnSpan(project, compact ? 2 : 1);
        Grid.SetColumn(contract, compact ? 0 : 1);
        Grid.SetRow(contract, compact ? 1 : 0);
        Grid.SetColumnSpan(contract, compact ? 2 : 1);
        project.Margin = compact ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 12, 0);
        contract.Margin = compact ? new Thickness(0) : new Thickness(12, 0, 0, 0);
    }

    private void RepresentativeCards_SizeChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not ItemsControl cards) return;
        var grid = FindChild<UniformGrid>(cards);
        if (grid == null) return;
        var columns = cards.ActualWidth < 1100 ? 1 : 3;
        if (grid.Columns != columns) grid.Columns = columns;
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            var nested = FindChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private Point _dragStartPoint;

    private void RepresentativesGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Danh sach nay doi trang bang pager, khong cho DataGrid dich noi dung trong mot trang.
        e.Handled = true;
    }

    private void RepresentativesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void RepresentativesGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not DataGrid grid) return;

        var delta = e.GetPosition(null) - _dragStartPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (FindParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is not ContractRepresentative representative) return;
        DragDrop.DoDragDrop(grid, representative, DragDropEffects.Move);
    }

    private void RepresentativesGrid_Drop(object sender, DragEventArgs e)
    {
        if (sender is not DataGrid grid || grid.DataContext is not TestRepresentativeGroup group ||
            !e.Data.GetDataPresent(typeof(ContractRepresentative))) return;

        var source = e.Data.GetData(typeof(ContractRepresentative)) as ContractRepresentative;
        var target = FindParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as ContractRepresentative;
        if (source == null || target == null || ReferenceEquals(source, target)) return;

        var oldIndex = group.Members.IndexOf(source);
        var newIndex = group.Members.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0) return;

        group.Members.Move(oldIndex, newIndex);
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T result) return result;
            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}
