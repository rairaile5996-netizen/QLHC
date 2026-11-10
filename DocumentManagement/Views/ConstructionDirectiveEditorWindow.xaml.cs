using System.Windows;
using DocumentManagement.Models;
using DocumentManagement.Utilities;

namespace DocumentManagement.Views;

public partial class ConstructionDirectiveEditorWindow : Window
{
    private readonly Action<ConstructionDirective> _save;
    public ConstructionDirectiveEditorWindow(ConstructionDirective item, Action<ConstructionDirective> save)
    {
        InitializeComponent();
        DataContext = item;
        _save = save;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ReceivedPicker.SelectedDate == null || System.Windows.Controls.Validation.GetHasError(ReceivedPicker))
                throw new InvalidOperationException("Vui lòng chọn ngày nhận hợp lệ.");
            var item = (ConstructionDirective)DataContext;
            item.ReceivedDate = ReceivedPicker.SelectedDate.Value;
            _save(item);
            DialogResult = true;
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
    }
}
