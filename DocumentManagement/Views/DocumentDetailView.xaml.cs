using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace DocumentManagement.Views;

public partial class DocumentDetailView : System.Windows.Controls.UserControl
{
    public DocumentDetailView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (_compactSummaryNotes == true && e.NewValue is ViewModels.MainViewModel viewModel)
                viewModel.IsNotesExpanded = false;
        };
    }

    private bool? _compactDetailLayout;
    private bool? _compactSummaryCards;
    private bool? _compactSummaryNotes;
    private int _summaryMetricsColumns = 4;

    private void DocumentDetailView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Four fixed columns truncate large currency values on smaller displays.
        var metricsColumns = e.NewSize.Width < 440 ? 1 : e.NewSize.Width < 800 ? 2 : 4;
        if (_summaryMetricsColumns != metricsColumns)
        {
            _summaryMetricsColumns = metricsColumns;
            SummaryMetricsGrid.Columns = metricsColumns;
            SummaryMetricsGrid.Rows = 4 / metricsColumns;
        }

        var compact = e.NewSize.Width < 1020;
        if (_compactDetailLayout != compact)
        {
            _compactDetailLayout = compact;
            Grid.SetRow(ChecklistSaveButton, compact ? 1 : 0);
            Grid.SetColumn(ChecklistSaveButton, compact ? 0 : 1);
            Grid.SetColumnSpan(ChecklistSaveButton, compact ? 2 : 1);
            ChecklistSaveButton.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
            ChecklistSaveButton.Margin = compact ? new Thickness(0, 12, 0, 0) : new Thickness(0);

            Grid.SetRow(PaymentCard, compact ? 1 : 0);
            Grid.SetColumn(PaymentCard, compact ? 0 : 1);
            Grid.SetColumnSpan(PaymentCard, compact ? 2 : 1);
            Grid.SetColumnSpan(AdvanceReconciliationCard, compact ? 2 : 1);
            AdvanceReconciliationCard.Margin = compact ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 8, 0);
            PaymentCard.Margin = compact ? new Thickness(0) : new Thickness(8, 0, 0, 0);
        }

        var narrowNotes = e.NewSize.Width < 1100;
        if (_compactSummaryNotes != narrowNotes)
        {
            _compactSummaryNotes = narrowNotes;
            if (narrowNotes && DataContext is ViewModels.MainViewModel viewModel)
                viewModel.IsNotesExpanded = false;
            SummaryNotesColumn.Width = narrowNotes ? new GridLength(0) : GridLength.Auto;
            Grid.SetRow(SummaryNotesPanel, narrowNotes ? 1 : 0);
            Grid.SetColumn(SummaryNotesPanel, narrowNotes ? 0 : 1);
            Grid.SetColumnSpan(SummaryNotesPanel, narrowNotes ? 2 : 1);
            SummaryNotesPanel.Margin = narrowNotes ? new Thickness(16, 12, 16, 0) : new Thickness(12, 0, 0, 0);
            SummaryNotesPanel.MinHeight = narrowNotes ? 48 : 0;
            SummaryNotesPanel.MaxHeight = narrowNotes ? 220 : double.PositiveInfinity;
            SummaryNotesContent.Width = narrowNotes ? double.NaN : 280;
            SummaryNotesEditor.MinHeight = narrowNotes ? 60 : 120;
            SummaryNotesRibbon.Height = narrowNotes ? 48 : double.NaN;
            SummaryNotesRibbon.VerticalAlignment = narrowNotes ? VerticalAlignment.Top : VerticalAlignment.Stretch;
            SummaryNotesRibbonControls.Margin = narrowNotes ? new Thickness(0, 6, 0, 0) : new Thickness(0, 24, 0, 0);
            SummaryNotesCollapsedLabel.Height = narrowNotes ? 0 : double.NaN;
            SummaryNotesCollapsedLabel.Margin = narrowNotes ? new Thickness(0) : new Thickness(0, 40, 0, 0);
            SummaryDetailsScroller.Margin = narrowNotes ? new Thickness(0) : new Thickness(0, 0, 12, 0);
        }

        var narrowCards = e.NewSize.Width < 740;
        if (_compactSummaryCards == narrowCards) return;
        _compactSummaryCards = narrowCards;
        Grid.SetRow(SummaryFinanceCards, narrowCards ? 1 : 0);
        Grid.SetColumn(SummaryFinanceCards, narrowCards ? 0 : 1);
        Grid.SetColumnSpan(SummaryProjectCards, narrowCards ? 2 : 1);
        Grid.SetColumnSpan(SummaryFinanceCards, narrowCards ? 2 : 1);
        SummaryProjectCards.Margin = narrowCards ? new Thickness(0) : new Thickness(0, 0, 8, 0);
        SummaryFinanceCards.Margin = narrowCards ? new Thickness(0) : new Thickness(8, 0, 0, 0);
    }

    private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
    {
        e.Handled = NumberRegex().IsMatch(e.Text);
    }

    [GeneratedRegex("[^0-9]+")]
    private static partial Regex NumberRegex();

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

    private void DetailPanel_Drop(object sender, System.Windows.DragEventArgs e)
    {
        HandleDrop(e);
    }

    private void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
    {
        HandleDrop(e);
    }

    private void HandleDrop(System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                if (DataContext is ViewModels.MainViewModel viewModel)
                {
                    viewModel.HandleFileDrop(files);
                }
            }
        }
    }
}

