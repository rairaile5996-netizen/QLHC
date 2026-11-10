using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DocumentManagement.Models;

namespace DocumentManagement.Converters;

/// <summary>Displays the empty search state using the rows' existing visibility flags.</summary>
public sealed class CvxdNoResultsVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // Count and SearchText bindings refresh this display after collection/search changes.
        if (values.Length == 0 || values[0] is not IEnumerable<CvxdItem> rows)
            return Visibility.Collapsed;

        return rows.Any() && !rows.Any(row => row.IsVisible)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("This display binding is one-way.");
}
