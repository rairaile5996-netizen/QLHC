using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Threading;
using DocumentManagement.Models;
using DocumentManagement.Services;
using DocumentManagement.Utilities;
using DocumentManagement.ViewModels;
using DocumentManagement.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DocumentManagement.Tests;

public sealed class ConstructionDirectivesWindowTests
{
    [Fact]
    public void PopulatedWindowRendersAndSelectsRowsWithApplicationStyles()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "directive-window-" + Guid.NewGuid().ToString("N"));
            Application? app = null;
            try
            {
                app = new Application();
                app.Resources.MergedDictionaries.Add(new MaterialDesignThemes.Wpf.BundledTheme
                {
                    BaseTheme = MaterialDesignThemes.Wpf.BaseTheme.Dark,
                    PrimaryColor = MaterialDesignColors.PrimaryColor.DeepPurple,
                    SecondaryColor = MaterialDesignColors.SecondaryColor.Lime
                });
                foreach (var uri in new[]
                {
                    "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign2.Defaults.xaml",
                    "pack://application:,,,/DocumentManagement;component/Styles/Converters.xaml",
                    "pack://application:,,,/DocumentManagement;component/Styles/SharedStyles.xaml"
                }) app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri) });
                // Load the PDVT view at runtime as well: WPF accepts some invalid
                // property values at build time and only throws during InitializeComponent.
                Assert.NotNull(new PdvtView());
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.DispatcherUnhandledException += (_, e) => { failure ??= e.Exception; e.Handled = true; };
                Directory.CreateDirectory(root);
                var source = Path.Combine(root, "ABC.2026 - Scan.pdf");
                File.WriteAllText(source, "test scan");
                var service = new ConstructionDirectiveService(Path.Combine(root, "store"));
                service.Import(source, ConstructionDirectiveService.ParseFileName(source));
                service.Import(source, new ConstructionDirective { Number = "DEF.2026", Title = "Second scan" });
                var services = new ServiceCollection();
                typeof(DocumentManagement.App).GetMethod("ConfigureServices", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, new object[] { services });
                services.AddSingleton(service);
                using var provider = services.BuildServiceProvider();
                var pdvtVm = new PdvtViewModel(provider.GetRequiredService<MainViewModel>())
                {
                    // Discard unsaved sheets instead of opening a modal dialog.
                    Ask = (_, _, _) => MessageBoxResult.No
                };
                var pdvtView = new PdvtView { DataContext = pdvtVm, Height = 600 };
                pdvtVm.NewCommand.Execute(null);
                var pdvtHost = new Window { Content = new ScrollViewer { Content = pdvtView }, Width = 1300, Height = 720, ShowInTaskbar = false };
                pdvtHost.Show();
                pdvtHost.UpdateLayout();
                Assert.Equal("PDVT 01", pdvtVm.DraftNumber);
                Assert.Contains(FindVisuals<TextBox>(pdvtView), box => box.Text == "PDVT 01");
                pdvtVm.CloseEditorCommand.Execute(null);
                pdvtVm.BoqItems.Add(new PdvtBoqItem { NoiDung = "Van chặn lửa", IsSelected = true });
                pdvtVm.NewFromSelectedCommand.Execute(null);
                Assert.Equal("Van chặn lửa", Assert.Single(pdvtVm.DraftMaterials).NoiDung);
                Assert.Equal("PDVT 01", pdvtVm.DraftNumber);
                pdvtVm.CloseEditorCommand.Execute(null);
                for (var i = 0; i < 83; i++) pdvtVm.BoqItems.Add(new PdvtBoqItem { NoiDung = $"Vật tư {i}" });
                pdvtVm.ShowSummaryCommand.Execute(null);
                pdvtHost.UpdateLayout();
                var boqGrid = FindVisuals<DataGrid>(pdvtView).Single(grid => ReferenceEquals(grid.ItemsSource, pdvtVm.SummaryItems));
                var boqScroll = FindVisuals<ScrollViewer>(boqGrid).First();
                Assert.True(boqScroll.ScrollableHeight > 0, "Bảng tổng hợp BoQ phải cuộn riêng khi có nhiều vật tư.");
                Assert.True(boqScroll.ScrollableWidth >= 0, "Bảng tổng hợp BoQ phải hiển thị được.");
                var createButton = FindVisuals<Button>(pdvtView).Single(button => ReferenceEquals(button.Command, pdvtVm.NewFromSelectedCommand));
                Assert.True(createButton.IsVisible, "Nút lập phiếu từ dòng chọn phải hiện ở màn tổng hợp.");
                var buttonBottom = createButton.TransformToAncestor(pdvtView).Transform(new Point(0, createButton.ActualHeight)).Y;
                Assert.True(buttonBottom <= pdvtView.ActualHeight, "Nút tạo PDVT phải nằm trong chiều cao màn PDVT.");
                pdvtVm.NewCommand.Execute(null);
                Assert.False(pdvtVm.IsSummaryView);
                for (var i = 0; i < 100; i++) pdvtVm.DraftMaterials.Add(new PdvtMaterialRow { Stt = i + 1, NoiDung = $"Vật tư {i}" });
                pdvtHost.UpdateLayout();
                var draftGrid = FindVisuals<DataGrid>(pdvtView).Single(grid => ReferenceEquals(grid.ItemsSource, pdvtVm.DraftMaterials));
                var draftScroll = FindVisuals<ScrollViewer>(draftGrid).First();
                Assert.True(draftScroll.ScrollableHeight > 0, "Bảng tạo PDVT phải cuộn riêng khi có 100 vật tư.");
                pdvtVm.PickFromBoqCommand.Execute(null);
                pdvtHost.UpdateLayout();
                Assert.True(pdvtVm.IsBoqDrawerOpen);
                Assert.Contains(FindVisuals<DataGrid>(pdvtView), grid => ReferenceEquals(grid.ItemsSource, pdvtVm.DrawerBoqItems) && grid.IsVisible);
                pdvtVm.CloseDrawerCommand.Execute(null);
                pdvtVm.CloseEditorCommand.Execute(null);
                pdvtHost.Close();
                foreach (var dark in new[] { true, false })
                {
                    AppThemeResources.Apply(dark);
                    var window = provider.GetRequiredService<ConstructionDirectivesWindow>();
                    window.ShowInTaskbar = false;
                    var vm = (ConstructionDirectivesViewModel)window.DataContext;
                    // Model module entry: the selector switches MainWindow before opening.
                    app.MainWindow = window;
                    try
                    {
                        window.Show();
                        window.UpdateLayout();
                        app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        var grid = FindGrid(window);
                        Assert.Equal(DataGridSelectionUnit.FullRow, grid.SelectionUnit);
                        Assert.Equal(2, grid.Items.Count);
                        grid.SelectedItem = vm.Items[1];
                        grid.UpdateLayout();
                        app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        Assert.Same(vm.Items[1], vm.SelectedDirective);
                        Assert.True(((DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(vm.Items[1])).IsSelected);
                        var search = (TextBox)window.FindName("SearchBox");
                        search.Text = "ABC";
                        window.UpdateLayout();
                        var host = (ScrollViewer)search.Template.FindName("PART_ContentHost", search);
                        Assert.Equal(VerticalAlignment.Center, host.VerticalAlignment);
                        var hostPosition = host.TranslatePoint(new Point(), search);
                        Assert.InRange(Math.Abs(hostPosition.Y + host.ActualHeight / 2 - search.ActualHeight / 2), 0, 2);
                        search.Text = "";
                        vm.Reload(service);
                        window.UpdateLayout();
                        var year = (ComboBox)window.FindName("YearFilter");
                        Assert.Equal("Tất cả năm", year.SelectedItem);
                        var selectedText = FindVisual<TextBlock>(year, text => text.Name == "YearSelectionText");
                        var bodySize = (double)app.FindResource("Blueprint.FontSize.Body");
                        var bodyFont = (FontFamily)app.FindResource("Blueprint.Font.Body");
                        Assert.Equal(bodySize, search.FontSize);
                        Assert.Equal(bodySize, selectedText.FontSize);
                        Assert.Equal(bodyFont.Source, search.FontFamily.Source);
                        foreach (var text in FindVisuals<TextBlock>(window).Where(text =>
                            text.Text is "Tìm số hoặc tên chỉ thị…" or "Thêm scan" or "Lưu trữ và tra cứu nhanh file scan" ||
                            text.Name == "FooterStoragePath" || text.Text.StartsWith("Đã tìm thấy")))
                        {
                            Assert.Equal(bodySize, text.FontSize);
                            Assert.Equal(bodyFont.Source, text.FontFamily.Source);
                        }
                        Assert.Equal(((SolidColorBrush)app.FindResource("Blueprint.Surface")).Color,
                            ((SolidColorBrush)window.Background).Color);
                        Assert.Equal(((SolidColorBrush)app.FindResource("Blueprint.OnPrimary")).Color,
                            ((SolidColorBrush)FindVisual<TextBlock>(window, text => text.Text == "Thêm scan").Foreground).Color);
                        Assert.Equal("Tất cả năm", selectedText.Text);
                        year.SelectedItem = "2026";
                        window.UpdateLayout();
                        Assert.Equal("2026", selectedText.Text);
                        vm.Reload(service);
                        window.UpdateLayout();
                        Assert.Equal("2026", selectedText.Text);
                        year.SelectedItem = "Tất cả năm";
                        window.UpdateLayout();
                        Assert.Equal("Tất cả năm", selectedText.Text);
                        Assert.DoesNotContain(grid.Columns, column => Equals(column.Header, "Công trình"));
                        var toggle = (ToggleButton)year.Template.FindName("YearToggle", year);
                        var togglePeer = new ToggleButtonAutomationPeer(toggle);
                        ((IToggleProvider)togglePeer.GetPattern(PatternInterface.Toggle)!).Toggle();
                        app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                        Assert.True(year.IsDropDownOpen);
                        Assert.InRange(Math.Abs(toggle.ActualWidth - year.ActualWidth), 0, 2);
                        year.IsDropDownOpen = false;
                        var firstRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
                        var cellText = (TextBlock)grid.Columns[0].GetCellContent(firstRow);
                        Assert.Equal(bodySize, cellText.FontSize);
                        Assert.Equal(VerticalAlignment.Center, cellText.VerticalAlignment);
                        Assert.Equal(TextAlignment.Left, cellText.TextAlignment);
                        var cell = (DataGridCell)cellText.Parent;
                        var textPosition = cellText.TranslatePoint(new Point(), cell);
                        Assert.InRange(Math.Abs(textPosition.Y + cellText.ActualHeight / 2 - cell.ActualHeight / 2), 0, 2);
                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        SaveScreenshot(bitmap, $"directives-{(dark ? "dark" : "light")}.png");
                        var editor = new ConstructionDirectiveEditorWindow(new ConstructionDirective
                        {
                            Number = "326.2026", Title = "Thực hiện công việc sửa chữa", ReceivedDate = new DateTime(2026, 9, 14)
                        }, _ => { });
                        editor.ShowInTaskbar = false;
                        try
                        {
                            editor.Show();
                            editor.UpdateLayout();
                            app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                            Assert.Null(editor.FindName("StatusPicker"));
                            Assert.DoesNotContain(FindVisuals<TextBox>(editor), box =>
                                System.Windows.Data.BindingOperations.GetBinding(box, TextBox.TextProperty)?.Path.Path == "Project");
                            var picker = (DatePicker)editor.FindName("ReceivedPicker");
                            var dateInput = (DatePickerTextBox)picker.Template.FindName("PART_TextBox", picker);
                            Assert.Equal(bodySize, dateInput.FontSize);
                            Assert.Equal(((SolidColorBrush)app.FindResource("Blueprint.SurfaceContainer")).Color,
                                ((SolidColorBrush)editor.Background).Color);
                            foreach (var button in FindVisuals<Button>(editor).Where(button => button.Content is string))
                                Assert.Equal(bodySize, button.FontSize);
                            Assert.Equal("14/09/2026", dateInput.Text);
                            Assert.Equal(((SolidColorBrush)editor.FindResource("Blueprint.SurfaceContainer")).Color,
                                ((SolidColorBrush)dateInput.Background).Color);
                            dateInput.Text = "15/09/2026";
                            picker.SelectedDate = DateTime.ParseExact(dateInput.Text, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                            Assert.Equal(new DateTime(2026, 9, 15), ((ConstructionDirective)editor.DataContext).ReceivedDate);
                            picker.IsDropDownOpen = true;
                            editor.UpdateLayout();
                            app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                            var popup = (Popup)picker.Template.FindName("PART_Popup", picker);
                            Assert.True(popup.IsOpen);
                            var calendar = (Calendar)popup.Child;
                            Assert.Equal(((SolidColorBrush)editor.FindResource("Blueprint.SurfaceBright")).Color,
                                ((SolidColorBrush)calendar.Background).Color);
                            var calendarBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(calendar.ActualWidth), (int)Math.Ceiling(calendar.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                            calendarBitmap.Render(calendar);
                            SaveScreenshot(calendarBitmap, $"directive-calendar-{(dark ? "dark" : "light")}.png");
                            picker.IsDropDownOpen = false;
                            var editorBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(560, 510, 96, 96, PixelFormats.Pbgra32);
                            editorBitmap.Render(editor);
                            SaveScreenshot(editorBitmap, $"directive-editor-{(dark ? "dark" : "light")}.png");
                            AppThemeResources.Apply(!dark);
                            editor.UpdateLayout();
                            window.UpdateLayout();
                            Assert.Equal(((SolidColorBrush)app.FindResource("Blueprint.SurfaceContainer")).Color,
                                ((SolidColorBrush)editor.Background).Color);
                            Assert.Equal(((SolidColorBrush)app.FindResource("Blueprint.OnSurface")).Color,
                                ((SolidColorBrush)selectedText.Foreground).Color);
                            AppThemeResources.Apply(dark);

                        }
                        finally { editor.Close(); }
                        Assert.Null(failure);
                    }
                    finally
                    {
                        // Model returning to selector: close must not shut down the app.
                        app.MainWindow = new Window();
                        window.Close();
                        Assert.False(app.Dispatcher.HasShutdownStarted);
                    }
                }
            }
            catch (Exception ex) { failure ??= ex; }
            finally
            {
                app?.Shutdown();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "WPF smoke test timed out.");
        Assert.Null(failure);
    }

    private static DataGrid FindGrid(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is DataGrid grid) return grid;
            try { return FindGrid(child); }
            catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException("Directive grid missing.");
    }

    private static IEnumerable<T> FindVisuals<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T matching) yield return matching;
            foreach (var descendant in FindVisuals<T>(child)) yield return descendant;
        }
    }
    private static T FindVisual<T>(DependencyObject parent, Func<T, bool> predicate) where T : DependencyObject =>
        FindVisuals<T>(parent).First(predicate);

    private static void SaveScreenshot(System.Windows.Media.Imaging.BitmapSource image, string name)
    {
        var folder = Path.Combine(Path.GetTempPath(), "directive-ui-review");
        Directory.CreateDirectory(folder);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using var stream = File.Create(Path.Combine(folder, name));
        encoder.Save(stream);
    }

}
