using System.Globalization;
using System.Text.RegularExpressions;

namespace DocumentManagement.Utilities;

public static partial class VietnameseFormat
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static bool TryParseMoney(string? value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim()
            .Replace("đ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("vnd", "", StringComparison.OrdinalIgnoreCase)
            .Replace("vnđ", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, VietnameseCulture, out result))
            return true;

        if (decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out result))
            return true;

        var digitsOnly = NonDigitRegex().Replace(text, "");
        return digitsOnly.Length > 0 && decimal.TryParse(digitsOnly, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    public static decimal ParseMoneyOrZero(string? value)
    {
        return TryParseMoney(value, out var result) ? result : 0;
    }

    [GeneratedRegex(@"[^\d-]")]
    private static partial Regex NonDigitRegex();
}
