using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DocumentManagement.Converters;

/// <summary>Binds a string/enum value to a RadioButton: checked when the value equals the parameter.</summary>
public sealed class EqualsToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter?.ToString() is { } text
            ? targetType == typeof(string) || targetType == typeof(object)
                ? text
                : System.ComponentModel.TypeDescriptor.GetConverter(targetType).ConvertFromInvariantString(text)!
            : Binding.DoNothing;
}

/// <summary>Visible when the value equals the parameter (Collapsed otherwise). Prefix the parameter with "!" to invert.</summary>
public sealed class EqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var expected = parameter?.ToString() ?? string.Empty;
        var invert = expected.StartsWith('!');
        if (invert) expected = expected[1..];
        var match = expected.Split('|').Contains(value?.ToString() ?? string.Empty);
        return match ^ invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Turns a count into a star GridLength so stacked progress bars can be built from Grid columns.</summary>
public sealed class CountToStarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new GridLength(value switch { int i => Math.Max(0, i), double d => Math.Max(0, d), _ => 0 }, GridUnitType.Star);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Heading level → left indent for the BoQ preview tree.</summary>
public sealed class LevelToIndentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new Thickness(value is int level && level > 1 ? (level - 1) * 16 : 0, 0, 0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// Lets elements outside the visual tree (DataGrid columns) bind to the view model:
/// declare it as a resource with Data="{Binding}" and bind via Source={StaticResource ...}.
/// </summary>
public sealed class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
