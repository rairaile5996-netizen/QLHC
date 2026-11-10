using System.Windows;
using System.Windows.Input;
using DocumentManagement.ViewModels;

namespace DocumentManagement.Views;

public partial class FindReplaceWindow : Window
{
    private readonly CvxdWindowViewModel _viewModel;
    private readonly bool _vtdvOnly;
    private readonly Func<string, int>? _count;
    private readonly Func<string, string, int>? _replace;
    private readonly Func<string, string, int>? _replaceAll;
    private readonly Action<string, bool>? _focus;
    private bool _hasFindResult;

    public FindReplaceWindow(CvxdWindowViewModel viewModel, bool vtdvOnly)
    {
        _viewModel = viewModel;
        _vtdvOnly = vtdvOnly;
        InitializeComponent();
    }

    public FindReplaceWindow(VtdvViewModel viewModel)
    {
        _viewModel = null!;
        _vtdvOnly = true;
        _count = viewModel.CountFindReplaceMatches;
        _replace = viewModel.ReplaceNext;
        _replaceAll = viewModel.ReplaceAll;
        InitializeComponent();
    }

    public FindReplaceWindow(CvxdWindowViewModel viewModel, bool vtdvOnly, Action<string, bool> focus)
        : this(viewModel, vtdvOnly)
    {
        _focus = focus;
    }

    public FindReplaceWindow(VtdvViewModel viewModel, Action<string, bool> focus)
        : this(viewModel)
    {
        _focus = focus;
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        var find = FindBox.Text;
        if (string.IsNullOrWhiteSpace(find))
        {
            ResultText.Text = "Nhập nội dung cần tìm.";
            return;
        }

        var count = _count?.Invoke(find) ?? _viewModel.CountFindReplaceMatches(find, _vtdvOnly);
        if (count == 0)
        {
            ResultText.Text = "Không tìm thấy trong biên bản đang chọn.";
            return;
        }

        _focus?.Invoke(find, _hasFindResult);
        _hasFindResult = true;
        ResultText.Text = $"Tìm thấy {count} vị trí trong biên bản đang chọn.";
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var find = FindBox.Text;
        if (string.IsNullOrWhiteSpace(find))
        {
            ResultText.Text = "Nhập nội dung cần tìm.";
            return;
        }

        var count = _replaceAll?.Invoke(find, ReplaceBox.Text) ?? _viewModel.ReplaceAll(find, ReplaceBox.Text, _vtdvOnly);
        ResultText.Text = count == 0 ? "Không còn vị trí phù hợp trong biên bản đang chọn." : $"Đã thay thế {count} vị trí trong biên bản đang chọn.";
    }

    private void ReplaceNext_Click(object sender, RoutedEventArgs e)
    {
        var find = FindBox.Text;
        if (string.IsNullOrWhiteSpace(find))
        {
            ResultText.Text = "Nhập nội dung cần tìm.";
            return;
        }

        var count = _replace?.Invoke(find, ReplaceBox.Text) ?? _viewModel.ReplaceNext(find, ReplaceBox.Text, _vtdvOnly);
        ResultText.Text = count == 0 ? "Không còn vị trí phù hợp trong biên bản đang chọn." : "Đã thay thế 1 vị trí.";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    public void FocusReplaceInput()
    {
        ReplaceBox.Focus();
        ReplaceBox.SelectAll();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
