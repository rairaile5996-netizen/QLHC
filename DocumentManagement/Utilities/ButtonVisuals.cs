using System.Windows;
using System.Windows.Media;

namespace DocumentManagement.Utilities;

/// <summary>Theme-aware state brushes shared by the button templates.</summary>
public static class ButtonVisuals
{
    public static readonly DependencyProperty HoverBackgroundProperty =
        DependencyProperty.RegisterAttached("HoverBackground", typeof(Brush), typeof(ButtonVisuals));

    public static readonly DependencyProperty PressedBackgroundProperty =
        DependencyProperty.RegisterAttached("PressedBackground", typeof(Brush), typeof(ButtonVisuals));

    public static Brush? GetHoverBackground(DependencyObject element) =>
        (Brush?)element.GetValue(HoverBackgroundProperty);

    public static void SetHoverBackground(DependencyObject element, Brush? value) =>
        element.SetValue(HoverBackgroundProperty, value);

    public static Brush? GetPressedBackground(DependencyObject element) =>
        (Brush?)element.GetValue(PressedBackgroundProperty);

    public static void SetPressedBackground(DependencyObject element, Brush? value) =>
        element.SetValue(PressedBackgroundProperty, value);
}
