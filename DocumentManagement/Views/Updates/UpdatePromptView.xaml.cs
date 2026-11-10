using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using DocumentManagement.Services;
namespace DocumentManagement.Views.Updates;
public partial class UpdatePromptView : UserControl
{
 public event EventHandler? DownloadRequested; public event EventHandler? LaterRequested;
 public UpdatePromptView(){InitializeComponent();}
 public void Configure(string version, string current, long? size, IReadOnlyList<UpdateChange> changes, string notes)
 {
  CurrentVersionText.Text = current;
  NewVersionText.Text = version;
  PackageSizeText.Text = size is > 0 ? $"{size.Value / 1024d / 1024d:0.0} MB" : "Không xác định";

  var list = changes.Count > 0
   ? changes
   : new[] { new UpdateChange { Title = "Thông tin phát hành", Description = notes, Icon = "InformationOutline", Color = "#3B82F6" } };

  foreach (var c in list)
  {
   var row = new Grid { MinHeight = 32, Margin = new Thickness(0, 0, 0, 12) };
   row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
   row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

   var icon = new PackIcon
   {
    Kind = Enum.TryParse<PackIconKind>(c.Icon, true, out var kind) ? kind : PackIconKind.InformationOutline,
    Width = 18,
    Height = 18,
    HorizontalAlignment = HorizontalAlignment.Center,
    VerticalAlignment = VerticalAlignment.Center,
    Foreground = (Brush)new BrushConverter().ConvertFromString(c.Color)!
   };

   var iconContainer = new Border
   {
    Width = 32,
    Height = 32,
    HorizontalAlignment = HorizontalAlignment.Left,
    VerticalAlignment = VerticalAlignment.Top,
    CornerRadius = new CornerRadius(8),
    Child = icon
   };
   iconContainer.SetResourceReference(BackgroundProperty, "Blueprint.PrimaryContainer");
   row.Children.Add(iconContainer);

   var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
   var title = new TextBlock { Text = c.Title, FontWeight = FontWeights.SemiBold };
   title.SetResourceReference(TextBlock.ForegroundProperty, "Blueprint.OnSurface");
   var description = new TextBlock { Text = c.Description, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
   description.SetResourceReference(TextBlock.ForegroundProperty, "Blueprint.OnSurfaceVariant");
   text.Children.Add(title);
   text.Children.Add(description);
   Grid.SetColumn(text, 1);
   row.Children.Add(text);
   ChangesPanel.Children.Add(row);
  }
 }
 private void Download_Click(object s,RoutedEventArgs e)=>DownloadRequested?.Invoke(this,EventArgs.Empty); private void Later_Click(object s,RoutedEventArgs e)=>LaterRequested?.Invoke(this,EventArgs.Empty);
}
