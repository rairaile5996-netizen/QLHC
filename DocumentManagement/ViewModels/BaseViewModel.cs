using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DocumentManagement.Utilities;
using MaterialDesignThemes.Wpf;

namespace DocumentManagement.ViewModels;

/// <summary>
/// Base ViewModel with common properties for theme and UI state.
/// </summary>
public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private SolidColorBrush _backgroundColor = new(Color.FromRgb(0xF1, 0xF5, 0xF9));

    [ObservableProperty]
    private SolidColorBrush _cardBackgroundColor = new(Colors.White);

    [ObservableProperty]
    private SolidColorBrush _textPrimaryColor = new(Color.FromRgb(0x1E, 0x29, 0x3B));

    [ObservableProperty]
    private SolidColorBrush _textSecondaryColor = new(Color.FromRgb(0x64, 0x74, 0x8B));

    [ObservableProperty]
    private SolidColorBrush _borderColor = new(Color.FromRgb(0xE2, 0xE8, 0xF0));

    [ObservableProperty]
    private SolidColorBrush _lightThemeBorderColor = new(Color.FromRgb(0x3B, 0x82, 0xF6));

    [ObservableProperty]
    private SolidColorBrush _darkThemeBorderColor = new(Colors.Transparent);

    [ObservableProperty]
    private SolidColorBrush _contextMenuBackground = new(Colors.White);

    [ObservableProperty]
    private SolidColorBrush _contextMenuBorder = new(Color.FromRgb(0xE2, 0xE8, 0xF0));

    [ObservableProperty]
    private SolidColorBrush _contextMenuHover = new(Color.FromRgb(0xF1, 0xF5, 0xF9));

    [ObservableProperty]
    private SolidColorBrush _noteBackgroundColor = new(Color.FromRgb(0xFE, 0xF9, 0xC3));

    [ObservableProperty]
    private SolidColorBrush _noteBorderColor = new(Color.FromRgb(0xFD, 0xE0, 0x47));

    [ObservableProperty]
    private SolidColorBrush _noteTextColor = new(Color.FromRgb(0x78, 0x35, 0x0F));

    [ObservableProperty]
    private SolidColorBrush _noteInputBackgroundColor = new(Color.FromRgb(0xFF, 0xFE, 0xF0));

    [ObservableProperty]
    private ISnackbarMessageQueue _snackbarMessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

    [ObservableProperty]
    private string _currentThemeName = "Sáng";

    public void ShowToast(string message, bool isSuccess = true)
    {
        var prefix = isSuccess ? "✓ " : "✕ ";
        SnackbarMessageQueue?.Enqueue(prefix + message);
    }

    public virtual void ApplyTheme()
    {
        if (IsDarkTheme)
        {
            BackgroundColor = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
            CardBackgroundColor = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            TextPrimaryColor = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
            TextSecondaryColor = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            BorderColor = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
            CurrentThemeName = "Tối";
            LightThemeBorderColor = new SolidColorBrush(Colors.Transparent);
            DarkThemeBorderColor = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            NoteBackgroundColor = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            NoteBorderColor = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            NoteTextColor = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            NoteInputBackgroundColor = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
            ContextMenuBackground = new SolidColorBrush(Color.FromRgb(0x27, 0x34, 0x4D));
            ContextMenuBorder = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
            ContextMenuHover = new SolidColorBrush(Color.FromRgb(0x3B, 0x48, 0x5E));
        }
        else
        {
            BackgroundColor = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
            CardBackgroundColor = new SolidColorBrush(Colors.White);
            TextPrimaryColor = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            TextSecondaryColor = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            BorderColor = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
            CurrentThemeName = "Sáng";
            LightThemeBorderColor = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            DarkThemeBorderColor = new SolidColorBrush(Colors.Transparent);
            NoteBackgroundColor = new SolidColorBrush(Color.FromRgb(0xFE, 0xF9, 0xC3));
            NoteBorderColor = new SolidColorBrush(Color.FromRgb(0xFD, 0xE0, 0x47));
            NoteTextColor = new SolidColorBrush(Color.FromRgb(0x78, 0x35, 0x0F));
            NoteInputBackgroundColor = new SolidColorBrush(Color.FromRgb(0xFF, 0xFE, 0xF0));
            ContextMenuBackground = new SolidColorBrush(Colors.White);
            ContextMenuBorder = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
            ContextMenuHover = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
        }

        AppThemeResources.Apply(IsDarkTheme);
    }

    public void SetTheme(bool isDark)
    {
        IsDarkTheme = isDark;
        ApplyTheme();
    }
}
