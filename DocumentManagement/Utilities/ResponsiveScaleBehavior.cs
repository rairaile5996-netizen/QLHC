using System.Windows;
using System.Windows.Controls;

namespace DocumentManagement.Utilities;

/// <summary>
/// Keeps a window's visual layout proportional to its design canvas.
/// The child is measured at the design size and a Viewbox scales it as the
/// available window size changes, including text, controls and spacing.
/// </summary>
public static class ResponsiveScaleBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ResponsiveScaleBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty DesignWidthProperty =
        DependencyProperty.RegisterAttached("DesignWidth", typeof(double), typeof(ResponsiveScaleBehavior), new PropertyMetadata(1920d));

    public static readonly DependencyProperty DesignHeightProperty =
        DependencyProperty.RegisterAttached("DesignHeight", typeof(double), typeof(ResponsiveScaleBehavior), new PropertyMetadata(1080d));

    public static readonly DependencyProperty FillWindowProperty =
        DependencyProperty.RegisterAttached("FillWindow", typeof(bool), typeof(ResponsiveScaleBehavior), new PropertyMetadata(false));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetDesignWidth(DependencyObject element, double value) => element.SetValue(DesignWidthProperty, value);
    public static double GetDesignWidth(DependencyObject element) => (double)element.GetValue(DesignWidthProperty);
    public static void SetDesignHeight(DependencyObject element, double value) => element.SetValue(DesignHeightProperty, value);
    public static double GetDesignHeight(DependencyObject element) => (double)element.GetValue(DesignHeightProperty);
    public static void SetFillWindow(DependencyObject element, bool value) => element.SetValue(FillWindowProperty, value);
    public static bool GetFillWindow(DependencyObject element) => (bool)element.GetValue(FillWindowProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window) return;

        if ((bool)e.NewValue)
            window.Loaded += Window_Loaded;
        else
            window.Loaded -= Window_Loaded;
    }

    private static void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window || window.Content is Viewbox) return;
        if (window.Content is not FrameworkElement content) return;

        var designWidth = GetDesignWidth(window);
        var designHeight = GetDesignHeight(window);
        content.Width = designWidth;
        content.Height = designHeight;

        // Detach the content before assigning it to the Viewbox. WPF does not
        // allow a visual element to have two logical parents during startup.
        window.Content = null;

        var viewbox = new Viewbox
        {
            Stretch = GetFillWindow(window) ? System.Windows.Media.Stretch.Fill : System.Windows.Media.Stretch.Uniform,
            StretchDirection = StretchDirection.Both,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = content
        };

        window.Content = viewbox;
    }
}
