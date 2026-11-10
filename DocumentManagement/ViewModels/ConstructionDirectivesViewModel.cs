using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using DocumentManagement.Models;
using DocumentManagement.Services;

namespace DocumentManagement.ViewModels;

public partial class ConstructionDirectivesViewModel : ObservableObject
{
    public ObservableCollection<ConstructionDirective> Items { get; } = [];
    public ObservableCollection<string> YearOptions { get; } = ["Tất cả năm"];
    public ICollectionView Directives { get; }
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedYear = "Tất cả năm";
    [ObservableProperty] private ConstructionDirective? _selectedDirective;
    [ObservableProperty] private string _message = "Thêm file scan hoặc kéo thả PDF / ảnh vào danh sách.";
    public ConstructionDirectivesViewModel()
    {
        Directives = CollectionViewSource.GetDefaultView(Items);
        Directives.Filter = MatchesFilter;
    }
    partial void OnSearchTextChanged(string value) => Directives.Refresh();
    partial void OnSelectedYearChanged(string value)
    {
        // ComboBox clears its selection while ItemsSource is being created.
        // Keep that transient UI value equivalent to the all-years filter.
        if (string.IsNullOrWhiteSpace(value))
        {
            SelectedYear = "Tất cả năm";
            return;
        }
        Directives.Refresh();
    }

    private bool MatchesFilter(object value)
    {
        if (value is not ConstructionDirective item) return false;
        var searchMatches = string.IsNullOrWhiteSpace(SearchText) || item.Number.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
            || item.Title.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);
        if (!searchMatches || SelectedYear == "Tất cả năm") return searchMatches;
        var year = ConstructionDirectiveService.TryGetDirectiveYear(item.Number, out var parsed)
            ? parsed
            : item.ReceivedDate.Year;
        return string.Equals(year.ToString(), SelectedYear, StringComparison.Ordinal);
    }
    public void Reload(ConstructionDirectiveService service)
    {
        var entries = service.Load();
        Items.Clear();
        foreach (var item in entries.OrderByDescending(x => x.ReceivedDate).ThenBy(x => x.Number)) Items.Add(item);
        var current = SelectedYear;
        var years = Items.Select(item => ConstructionDirectiveService.TryGetDirectiveYear(item.Number, out var parsed)
                ? parsed : item.ReceivedDate.Year)
            .Distinct().OrderByDescending(year => year).Select(year => year.ToString()).ToList();
        YearOptions.Clear();
        YearOptions.Add("Tất cả năm");
        foreach (var year in years) YearOptions.Add(year);
        SelectedYear = YearOptions.Contains(current) ? current : "Tất cả năm";
        OnPropertyChanged(nameof(SelectedYear));
        Directives.Refresh();
    }
}
