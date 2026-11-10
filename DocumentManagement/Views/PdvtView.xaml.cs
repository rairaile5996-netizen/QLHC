using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DocumentManagement.Views;

public partial class PdvtView : UserControl
{
    public PdvtView() => InitializeComponent();

    /// <summary>Opens a button's context menu under it on a normal left click.</summary>
    private void ShowMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.DataContext = button.DataContext;
        menu.IsOpen = true;
    }
}
