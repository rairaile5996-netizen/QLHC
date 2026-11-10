using System;
using System.Globalization;
using System.Windows.Data;

namespace DocumentManagement.Converters;

public class CleanAppendixNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string appendixNumber)
            return value;

        // Strip "PL ", "PL", "Phụ lục " (case-insensitive)
        string cleaned = appendixNumber
            .Replace("Phụ lục ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("PL ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("PL", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return cleaned;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
