using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DocumentManagement.Utilities;

public static class DirectiveMessageDialog
{
    public static void Show(Window owner, string title, string message, bool isError)
    {
        var window = new Window { Title = title, Owner = owner, Width = 500, MinHeight = 210, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Brushes.Transparent, WindowStyle = WindowStyle.None, AllowsTransparency = true, ShowInTaskbar = false };
        AppThemeResources.Apply(window.Resources, AppThemeResources.LoadSavedIsDarkTheme());
        var card = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(26), BorderThickness = new Thickness(1), BorderBrush = (Brush)window.TryFindResource("Blueprint.Outline")!, Background = (Brush)window.TryFindResource("Blueprint.SurfaceContainer")! };
        var panel = new Grid(); panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var content = new StackPanel(); content.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = (Brush)window.TryFindResource(isError ? "Blueprint.Error" : "Blueprint.OnSurface")! }); content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 22), Foreground = (Brush)window.TryFindResource("Blueprint.OnSurfaceVariant")! }); panel.Children.Add(content);
        var ok = new Button { Content = "Đã hiểu", MinWidth = 96, MinHeight = 38, HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true }; ok.Click += (_, _) => window.Close(); Grid.SetRow(ok, 1); panel.Children.Add(ok); card.Child = panel; window.Content = card; window.ShowDialog();
    }
}
