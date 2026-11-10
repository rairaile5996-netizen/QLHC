using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DocumentManagement.Views;

public sealed class VtdvWrappingTextColumn : DataGridTextColumn
{
    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var element = base.GenerateElement(cell, dataItem);
        if (element is TextBlock textBlock)
        {
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.TextTrimming = TextTrimming.None;
            BindingOperations.SetBinding(textBlock, FrameworkElement.WidthProperty,
                new Binding(nameof(FrameworkElement.ActualWidth))
                {
                    Source = cell,
                    Converter = CellTextWidthConverter.Instance
                });
        }
        return element;
    }

    private sealed class CellTextWidthConverter : IValueConverter
    {
        public static readonly CellTextWidthConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is double width && width > 2 ? width - 2 : 418d;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            System.Windows.Data.Binding.DoNothing;
    }
}
