using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DocumentManagement.ViewModels;

namespace DocumentManagement.Views;

public partial class GuaranteeLettersView : UserControl
{
    public GuaranteeLettersView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is MainViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedGuaranteeRow) &&
            sender is MainViewModel viewModel &&
            viewModel.SelectedGuaranteeRow != null)
        {
            Dispatcher.BeginInvoke(() => GuaranteeListBox.ScrollIntoView(viewModel.SelectedGuaranteeRow));
        }
    }

    private void GuaranteeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel ||
            GuaranteeListBox.SelectedItem is not GuaranteeListRow row ||
            row.Kind != GuaranteeListRowKind.Record)
        {
            return;
        }

        viewModel.SelectedGuaranteeRow = row;
    }

    private void ChainHeaderCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border { DataContext: GuaranteeListRow row } &&
            DataContext is MainViewModel viewModel)
        {
            viewModel.ToggleGuaranteeChainCommand.Execute(row);
            e.Handled = true;
        }
    }
}
