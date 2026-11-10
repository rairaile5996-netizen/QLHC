using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace DocumentManagement.Views;

public partial class SettingsPopup : System.Windows.Controls.UserControl
{
    public SettingsPopup()
    {
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
