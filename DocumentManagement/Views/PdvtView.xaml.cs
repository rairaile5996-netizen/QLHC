using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DocumentManagement.Views;

public partial class PdvtView : UserControl
{
    public PdvtView()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Opens a button's context menu under it on a normal left click.</summary>
    private void ShowMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.DataContext = button.DataContext;
        menu.IsOpen = true;
    }

    /// <summary>Editable brand picker: a click on the text opens the alternatives too.</summary>
    private void CellCombo_OpenOnClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ComboBox { HasItems: true, IsDropDownOpen: false, IsEnabled: true } combo) return;
        // Only the text part; the arrow already toggles the list itself.
        for (var node = e.OriginalSource as DependencyObject; node != null && node != combo; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is ToggleButton) return;
            if (node is TextBox) { combo.IsDropDownOpen = true; return; }
        }
    }

    /// <summary>Ctrl+S: push the value being typed (e.g. Khối lượng, bound on LostFocus) before the save runs.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.S || Keyboard.Modifiers != ModifierKeys.Control) return;
        if (Keyboard.FocusedElement is TextBox box) box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }
}
