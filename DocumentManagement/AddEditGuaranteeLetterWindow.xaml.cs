using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using DocumentManagement.Models;
using DocumentManagement.Services;
using Microsoft.Win32;

namespace DocumentManagement;

public class GuaranteeRelationOption
{
    public int? RelatedAppendixId { get; init; }
    public string Label { get; init; } = string.Empty;
}

public partial class GuaranteeTypeOption : ObservableObject
{
    public GuaranteeType Value { get; init; }
    public string Label { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;
}

public partial class GuaranteeEditorDialogModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _submitText = string.Empty;

    [ObservableProperty]
    private bool _requirePdf = true;

    [ObservableProperty]
    private bool _allowRelationChange = true;

    [ObservableProperty]
    private ObservableCollection<GuaranteeRelationOption> _relationOptions = [];

    [ObservableProperty]
    private GuaranteeRelationOption? _selectedRelation;

    public Func<int?, GuaranteeType, bool>? IsTypeTaken { get; set; }

    [ObservableProperty]
    private GuaranteeType _type = GuaranteeType.AdvancePayment;

    public bool IsRelationSelectionVisible => Type == GuaranteeType.AdvancePayment;

    partial void OnTypeChanged(GuaranteeType value)
    {
        if (value != GuaranteeType.AdvancePayment)
        {
            SelectedRelation = null;
        }

        OnPropertyChanged(nameof(IsRelationSelectionVisible));
    }

    [ObservableProperty]
    private string _letterNumber = string.Empty;

    [ObservableProperty]
    private string _issuingBank = string.Empty;

    [ObservableProperty]
    private string _guaranteeValueText = string.Empty;

    [ObservableProperty]
    private DateTime _issueDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _effectiveDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _expiryDate = DateTime.Today;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _sourcePdfPath = string.Empty;

    [ObservableProperty]
    private string _existingPdfPath = string.Empty;

    public ObservableCollection<GuaranteeTypeOption> TypeOptions { get; } =
    [
        new() { Value = GuaranteeType.AdvancePayment, Label = "Bảo lãnh tạm ứng" },
        new() { Value = GuaranteeType.Performance, Label = "Bảo lãnh thực hiện" },
        new() { Value = GuaranteeType.Warranty, Label = "Bảo lãnh bảo hành" }
    ];

    public string PdfStatusText =>
        !string.IsNullOrWhiteSpace(SourcePdfPath)
            ? $"PDF mới: {Path.GetFileName(SourcePdfPath)}"
            : RequirePdf
                ? "Chưa chọn PDF"
                : "Giữ nguyên PDF hiện có";

    public string ExistingPdfSummary =>
        string.IsNullOrWhiteSpace(ExistingPdfPath)
            ? "Chưa có PDF đã sao chép."
            : $"PDF hiện tại: {ExistingPdfPath}";

    partial void OnSourcePdfPathChanged(string value)
    {
        OnPropertyChanged(nameof(PdfStatusText));
        OnPropertyChanged(nameof(ExistingPdfSummary));
    }

    partial void OnExistingPdfPathChanged(string value)
    {
        OnPropertyChanged(nameof(PdfStatusText));
        OnPropertyChanged(nameof(ExistingPdfSummary));
    }

    partial void OnSelectedRelationChanged(GuaranteeRelationOption? value)
    {
        RefreshTypeAvailability();
    }

    public void RefreshTypeAvailability()
    {
        foreach (var option in TypeOptions)
        {
            option.IsEnabled = IsTypeTaken?.Invoke(SelectedRelation?.RelatedAppendixId, option.Value) != true;
        }

        var selectedOption = TypeOptions.FirstOrDefault(option => option.Value == Type);
        if (selectedOption?.IsEnabled != true)
        {
            var firstAvailable = TypeOptions.FirstOrDefault(option => option.IsEnabled);
            if (firstAvailable != null)
            {
                Type = firstAvailable.Value;
            }
        }
    }
}

public partial class AddEditGuaranteeLetterWindow : Window
{
    private readonly int _documentId;
    private readonly GuaranteeEditorDialogModel _model;

    public AddEditGuaranteeLetterWindow(GuaranteeEditorDialogModel model, int documentId)
    {
        InitializeComponent();
        _model = model;
        _documentId = documentId;
        DataContext = model;
    }

    public GuaranteeUpsertRequest? ResultRequest { get; private set; }

    private void BrowsePdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Title = "Chọn PDF bảo lãnh"
        };

        if (dialog.ShowDialog() == true)
        {
            _model.SourcePdfPath = dialog.FileName;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_model.Type == GuaranteeType.AdvancePayment && _model.SelectedRelation == null)
        {
            MessageBox.Show("Vui lòng chọn liên hệ bảo lãnh.", "Thiếu dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(_model.GuaranteeValueText, NumberStyles.Number, CultureInfo.CurrentCulture, out var guaranteeValue) &&
            !decimal.TryParse(_model.GuaranteeValueText, NumberStyles.Number, CultureInfo.InvariantCulture, out guaranteeValue))
        {
            MessageBox.Show("Giá trị bảo lãnh không hợp lệ.", "Thiếu dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_model.RequirePdf && string.IsNullOrWhiteSpace(_model.SourcePdfPath) && string.IsNullOrWhiteSpace(_model.ExistingPdfPath))
        {
            MessageBox.Show("Vui lòng chọn PDF bảo lãnh.", "Thiếu dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultRequest = new GuaranteeUpsertRequest
        {
            DocumentId = _documentId,
            RelatedAppendixId = _model.Type == GuaranteeType.AdvancePayment ? _model.SelectedRelation?.RelatedAppendixId : null,
            RelatedAppendixLabel = _model.Type == GuaranteeType.AdvancePayment ? _model.SelectedRelation?.Label : null,
            Type = _model.Type,
            LetterNumber = _model.LetterNumber,
            IssuingBank = _model.IssuingBank,
            GuaranteeValue = guaranteeValue,
            IssueDate = _model.IssueDate,
            EffectiveDate = _model.EffectiveDate,
            ExpiryDate = _model.ExpiryDate,
            Notes = _model.Notes,
            SourcePdfPath = string.IsNullOrWhiteSpace(_model.SourcePdfPath) ? null : _model.SourcePdfPath
        };

        DialogResult = true;
        Close();
    }
}
