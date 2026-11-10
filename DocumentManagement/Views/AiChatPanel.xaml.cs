using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocumentManagement.Views;

public partial class AiChatPanel : System.Windows.Controls.UserControl
{
    public AiChatPanel()
    {
        InitializeComponent();
    }

    private void ChatInputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Execute send command
            var vm = DataContext as dynamic;
            if (vm?.SendChatMessageCommand?.CanExecute(null) == true)
            {
                vm.SendChatMessageCommand.Execute(null);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Allow new line with Ctrl+Enter
            if (sender is TextBox textBox)
            {
                int caretIndex = textBox.CaretIndex;
                textBox.Text = textBox.Text.Insert(caretIndex, Environment.NewLine);
                textBox.CaretIndex = caretIndex + Environment.NewLine.Length;
            }
            e.Handled = true;
        }
    }
}
