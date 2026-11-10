using System.Windows;
using System.Windows.Controls;

namespace DocumentManagement.Views.Updates;

public partial class UpdateErrorView : UserControl
{
    public event EventHandler? RetryRequested;
    public event EventHandler? LaterRequested;

    public UpdateErrorView()
    {
        InitializeComponent();
    }

    public void Configure(string details)
        => DetailsText.Text = details;

    public void SetRetrying(bool value)
    {
        RetryButton.IsEnabled = !value;
        RetryButton.Content = value
            ? "Đang thử lại..."
            : "Thử lại";
    }

    private void Retry_Click(object s, RoutedEventArgs e)
        => RetryRequested?.Invoke(this, EventArgs.Empty);

    private void Later_Click(object s, RoutedEventArgs e)
        => LaterRequested?.Invoke(this, EventArgs.Empty);
}