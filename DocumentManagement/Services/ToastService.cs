using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DocumentManagement.Services;

/// <summary>
/// Custom Toast Service - đơn giản và hiệu quả hơn Snackbar
/// </summary>
public class ToastService
{
    private readonly Canvas _container;
    private readonly DispatcherTimer _timer;

    public ToastService(System.Windows.Controls.Panel container)
    {
        _container = container as Canvas ?? throw new ArgumentException("Container must be a Canvas");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += Timer_Tick;
    }

    public void ShowSuccess(string message)
    {
        ShowToast($"✅ {message}", "#10B981");
    }

    public void ShowError(string message)
    {
        ShowToast($"❌ {message}", "#EF4444");
    }

    public void ShowWarning(string message)
    {
        ShowToast($"⚠️ {message}", "#F59E0B");
    }

    public void ShowInfo(string message)
    {
        ShowToast($"ℹ️ {message}", "#3B82F6");
    }

    private void ShowToast(string message, string backgroundColor)
    {
        // Xóa toast cũ nếu có
        ClearExistingToast();

        // Tạo toast mới
        var toast = CreateToastElement(message, backgroundColor);
        
        // Thêm vào canvas với absolute positioning
        Canvas.SetTop(toast, 20);
        Canvas.SetLeft(toast, Math.Max(0, (_container.ActualWidth - 400) / 2)); // Center horizontally
        System.Windows.Controls.Panel.SetZIndex(toast, 99999);
        
        _container.Children.Add(toast);

        // Animation fade in
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        var slideIn = new DoubleAnimation(-20, 0, TimeSpan.FromMilliseconds(300));
        
        toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        toast.RenderTransform = new TranslateTransform();
        toast.RenderTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);

        // Auto hide after 3 seconds
        _timer.Stop();
        _timer.Start();
    }

    private static Border CreateToastElement(string message, string backgroundColor)
    {
        var border = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20, 14, 20, 14),
            MaxWidth = 400,
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 4,
                Opacity = 0.3,
                Color = Colors.Black
            }
        };

        var textBlock = new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        border.Child = textBlock;
        return border;
    }

    private void ClearExistingToast()
    {
        // Tìm và xóa toast cũ
        for (int i = _container.Children.Count - 1; i >= 0; i--)
        {
            if (_container.Children[i] is Border border && 
                border.Child is TextBlock textBlock &&
                (textBlock.Text.StartsWith('✅') || textBlock.Text.StartsWith('❌') || 
                 textBlock.Text.StartsWith("⚠️") || textBlock.Text.StartsWith("ℹ️")))
            {
                _container.Children.RemoveAt(i);
            }
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _timer.Stop();
        
        // Fade out animation
        for (int i = _container.Children.Count - 1; i >= 0; i--)
        {
            if (_container.Children[i] is Border border && 
                border.Child is TextBlock textBlock &&
                (textBlock.Text.StartsWith('✅') || textBlock.Text.StartsWith('❌') || 
                 textBlock.Text.StartsWith("⚠️") || textBlock.Text.StartsWith("ℹ️")))
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (s, e) => _container.Children.Remove(border);
                border.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }
    }
}