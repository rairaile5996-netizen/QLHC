using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DocumentManagement.Converters;

public class NullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToDialogTitleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "Chỉnh sửa hồ sơ" : "Thêm hợp đồng mới";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TabIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string paramStr && int.TryParse(paramStr, out int tabIndex))
        {
            return index == tabIndex;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is string paramStr && int.TryParse(paramStr, out int tabIndex))
        {
            return tabIndex;
        }
        return Binding.DoNothing;
    }
}

public class TabVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string paramStr && int.TryParse(paramStr, out int tabIndex))
        {
            return index == tabIndex ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TabBorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string paramStr && int.TryParse(paramStr, out int tabIndex))
        {
            return index == tabIndex 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)) // #3B82F6
                : System.Windows.Media.Brushes.Transparent;
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TabForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string paramStr && int.TryParse(paramStr, out int tabIndex))
        {
            return index == tabIndex 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)) // #3B82F6
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)); // #64748B
        }
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


public class ProgressWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal progress)
        {
            // Giáº£ sá»­ max width lÃ  khoáº£ng 400px, tÃ­nh theo %
            return (double)progress * 4; // 100% = 400px
        }
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TypeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;
        var targetTypeParam = parameter as Type;
        return value.GetType() == targetTypeParam ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


public class SortIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string paramStr && int.TryParse(paramStr, out int paramIndex))
        {
            return index == paramIndex;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr && int.TryParse(paramStr, out int paramIndex))
        {
            return paramIndex;
        }
        return Binding.DoNothing;
    }
}

// Converter Ä‘á»ƒ láº¥y icon theo loáº¡i file
public class FileTypeIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string ext)
        {
            return ext.ToUpperInvariant() switch
            {
                "XLSX" or "XLS" => "MicrosoftExcel",
                "DOCX" or "DOC" => "MicrosoftWord",
                "PDF" => "FilePdfBox",
                "DWG" or "DXF" => "FloorPlan",
                "ZIP" or "RAR" or "7Z" => "FolderZip",
                "JPG" or "JPEG" or "PNG" or "GIF" or "BMP" or "WEBP" => "Image",
                "MP4" or "AVI" or "MKV" or "MOV" => "Video",
                "MP3" or "WAV" or "FLAC" => "MusicNote",
                "TXT" => "FileDocumentOutline",
                "PPTX" or "PPT" => "MicrosoftPowerpoint",
                _ => "File"
            };
        }
        return "File";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Converter Ä‘á»ƒ láº¥y mÃ u icon theo loáº¡i file
public class FileTypeColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string ext)
        {
            return ext.ToUpperInvariant() switch
            {
                "XLSX" or "XLS" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)), // Green
                "DOCX" or "DOC" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)), // Blue
                "PDF" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)), // Red
                "DWG" or "DXF" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 92, 246)), // Purple
                "ZIP" or "RAR" or "7Z" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)), // Amber
                "JPG" or "JPEG" or "PNG" or "GIF" or "BMP" or "WEBP" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(236, 72, 153)), // Pink
                "MP4" or "AVI" or "MKV" or "MOV" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 85, 247)), // Purple
                "MP3" or "WAV" or "FLAC" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(14, 165, 233)), // Sky
                "PPTX" or "PPT" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 115, 22)), // Orange
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)) // Gray
            };
        }
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Converter Ä‘á»ƒ láº¥y mÃ u ná»n icon theo loáº¡i file
public class FileTypeBgColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string ext)
        {
            return ext.ToUpperInvariant() switch
            {
                "XLSX" or "XLS" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 250, 229)), // Green-100
                "DOCX" or "DOC" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(219, 234, 254)), // Blue-100
                "PDF" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 226, 226)), // Red-100
                "DWG" or "DXF" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(237, 233, 254)), // Purple-100
                "ZIP" or "RAR" or "7Z" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 243, 199)), // Amber-100
                "JPG" or "JPEG" or "PNG" or "GIF" or "BMP" or "WEBP" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(252, 231, 243)), // Pink-100
                "MP4" or "AVI" or "MKV" or "MOV" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 232, 255)), // Purple-100
                "MP3" or "WAV" or "FLAC" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 242, 254)), // Sky-100
                "PPTX" or "PPT" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 237, 213)), // Orange-100
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)) // Slate-100
            };
        }
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


/// <summary>
/// Chuyá»ƒn sá»‘ thÃ nh chá»¯ tiáº¿ng Viá»‡t
/// VÃ­ dá»¥: 817526194 â†’ "TÃ¡m trÄƒm mÆ°á»i báº£y triá»‡u, nÄƒm trÄƒm hai mÆ°Æ¡i sÃ¡u nghÃ¬n, má»™t trÄƒm chÃ­n mÆ°Æ¡i tÆ° Ä‘á»“ng"
/// </summary>
public static class NumberToVietnameseWords
{
    private static readonly string[] Ones = ["", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];
    private static readonly string[] Tens = ["", "mười", "hai mươi", "ba mươi", "bốn mươi", "năm mươi", "sáu mươi", "bảy mươi", "tám mươi", "chín mươi"];

    public static string Convert(decimal number)
    {
        if (number == 0) return "Không đồng";

        var isNegative = number < 0;
        number = Math.Abs(number);

        var intNumber = (long)Math.Round(number);
        var result = ConvertToWords(intNumber);

        if (!string.IsNullOrEmpty(result))
            result = char.ToUpper(result[0]) + result[1..];

        return (isNegative ? "Âm " : "") + result + " đồng";
    }

    private static string ConvertToWords(long number)
    {
        if (number == 0) return string.Empty;
        if (number < 0) return "âm " + ConvertToWords(-number);

        var parts = new List<string>();

        if (number / 1000000000000 > 0)
        {
            parts.Add(ConvertGroup(number / 1000000000000) + " nghìn tỷ");
            number %= 1000000000000;
        }

        if (number / 1000000000 > 0)
        {
            parts.Add(ConvertGroup(number / 1000000000) + " tỷ");
            number %= 1000000000;
        }

        if (number / 1000000 > 0)
        {
            parts.Add(ConvertGroup(number / 1000000) + " triệu");
            number %= 1000000;
        }

        if (number / 1000 > 0)
        {
            parts.Add(ConvertGroup(number / 1000) + " nghìn");
            number %= 1000;
        }

        if (number > 0)
            parts.Add(ConvertGroup(number));

        return string.Join(", ", parts);
    }

    private static string ConvertGroup(long number)
    {
        if (number == 0) return string.Empty;

        var result = string.Empty;

        if (number >= 100)
        {
            result += Ones[number / 100] + " trăm ";
            number %= 100;
        }

        if (number >= 10)
        {
            result += ConvertTens(number);
        }
        else if (number > 0)
        {
            if (result.Length > 0) result += "lẻ ";
            result += Ones[number];
        }

        return result.Trim();
    }

    private static string ConvertTens(long number)
    {
        var tens = number / 10;
        var ones = number % 10;
        var result = Tens[tens];

        if (ones > 0)
        {
            if (ones == 1 && tens > 1)
                result += " mốt";
            else if (ones == 5 && tens > 0)
                result += " lăm";
            else if (ones == 4 && tens > 1)
                result += " tư";
            else
                result += " " + Ones[ones];
        }

        return result;
    }
}
/// <summary>
/// Converter Ä‘á»ƒ hiá»ƒn thá»‹ menu item "Táº¡o CV thanh toÃ¡n" chá»‰ cho file Excel
/// </summary>
public class ExcelFileToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string ext)
        {
            var upper = ext.ToUpperInvariant();
            return (upper == "XLSX" || upper == "XLS") ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter Ä‘á»ƒ phÃ¢n loáº¡i message type dá»±a trÃªn icon
/// </summary>
public class MessageTypeConverter : IValueConverter
{
    public static readonly MessageTypeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string message)
        {
            if (message.StartsWith('✕') || message.StartsWith('❌')) return "Error";
            if (message.StartsWith("⚠")) return "Warning";
            if (message.StartsWith('ℹ')) return "Info";
            if (message.StartsWith('✓') || message.StartsWith('✅')) return "Success";
        }
        return "Success"; // Default
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter Ä‘á»ƒ chá»n RadioButton dá»±a trÃªn Enum
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        string checkValue = parameter.ToString() ?? "";
        return (value.ToString() ?? "") == checkValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            try
            {
                string? paramStr = parameter.ToString();
                if (paramStr != null)
                {
                    return Enum.Parse(targetType, paramStr);
                }
            }
            catch { }
        }
        return Binding.DoNothing;
    }
}
 
 public class GreaterThan0ToVisibilityConverter : IValueConverter
 {
     public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
     {
         if (value is decimal d) return d > 0 ? Visibility.Visible : Visibility.Collapsed;
         if (value is double db) return db > 0 ? Visibility.Visible : Visibility.Collapsed;
         if (value is int i) return i > 0 ? Visibility.Visible : Visibility.Collapsed;
         if (value is long l) return l > 0 ? Visibility.Visible : Visibility.Collapsed;
         return Visibility.Collapsed;
     }
 
     public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
 }
 
 public class LessOrEqual0ToVisibilityConverter : IValueConverter
 {
     public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
     {
         if (value is decimal d) return d <= 0 ? Visibility.Visible : Visibility.Collapsed;
         if (value is double db) return db <= 0 ? Visibility.Visible : Visibility.Collapsed;
         if (value is int i) return i <= 0 ? Visibility.Visible : Visibility.Collapsed;
         if (value is long l) return l <= 0 ? Visibility.Visible : Visibility.Collapsed;
         return Visibility.Visible;
     }
 
     public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
 }
 
 public class StringVisibilityConverter : IValueConverter
 {
     public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
     {
         return !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
     }
 
     public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
     {
         throw new NotImplementedException();
     }
 }
 
 public class BoolToEyeIconConverter : IValueConverter
 {
     public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
     {
         return value is true ? "EyeOffOutline" : "EyeOutline";
     }
 
     public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
     {
         throw new NotImplementedException();
     }
 }

public class BooleanToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        var trueValue = 1.0;
        var falseValue = 0.0;

        if (parameter is string paramStr)
        {
            var parts = paramStr.Split(':');
            if (parts.Length == 2)
            {
                double.TryParse(parts[0], out trueValue);
                double.TryParse(parts[1], out falseValue);
            }
            else if (double.TryParse(paramStr, out var singleValue))
            {
                falseValue = singleValue;
            }
        }

        return boolValue ? trueValue : falseValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class CollectionCountToScrollVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var maximumVisibleRows = int.TryParse(parameter?.ToString(), out var parsed) ? parsed : 3;
        var itemCount = value is int count ? count : 0;

        // With up to the configured row count, wheel scrolling should do nothing.
        return itemCount > maximumVisibleRows
            ? System.Windows.Controls.ScrollBarVisibility.Hidden
            : System.Windows.Controls.ScrollBarVisibility.Disabled;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

