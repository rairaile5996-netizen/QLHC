using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentManagement.Models;
using DocumentManagement.Services;
using DocumentManagement.Utilities;

namespace DocumentManagement.ViewModels;

/// <summary>
/// ViewModel for Settings - Theme, API Keys, Folder Path
/// </summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly AIService _AIService;
    private readonly Action? _onSettingsChanged;
    private readonly Func<Task>? _onFolderChanged;

    // Settings visibility
    [ObservableProperty]
    private bool _isSettingsOpen;

    // Base Folder Path
    [ObservableProperty]
    private string _baseFolderPath = @"D:\HỒ SƠ";


    [ObservableProperty]
    private bool _isUserMenuOpen;

    // Gemini API Key
    [ObservableProperty]
    private string _geminiApiKey = string.Empty;

    // Danh sách key với số thứ tự để hiển thị
    [ObservableProperty]
    private ObservableCollection<ApiKeyItem> _geminiApiKeyList = [];

    // Số lượng key
    [ObservableProperty]
    private int _geminiApiKeyCount;

    // Gemini Model
    [ObservableProperty]
    private string _selectedGeminiModel = "gemini-2.5-flash";

    public static string[] GeminiModels => AIService.GeminiModels;

    public SettingsViewModel(AIService AIService, Action? onSettingsChanged = null, Func<Task>? onFolderChanged = null)
    {
        _AIService = AIService;
        _onSettingsChanged = onSettingsChanged;
        _onFolderChanged = onFolderChanged;

        LoadSettings();
        LoadGeminiApiKey();
        LoadGeminiModel();
    }


    partial void OnGeminiApiKeyChanged(string value)
    {
        UpdateApiKeyList();
    }

    partial void OnSelectedGeminiModelChanged(string value)
    {
        SaveGeminiModel();
    }

    private void UpdateApiKeyList()
    {
        var lines = GeminiApiKey.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        GeminiApiKeyList.Clear();
        int displayIndex = 1;
        for (int i = 0; i < lines.Count; i++)
        {
            var parts = lines[i].Split(['\t', ';', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            string name, key;
            if (parts.Length >= 2)
            {
                name = parts[0].Trim();
                key = parts[1].Trim();
            }
            else
            {
                name = $"Key {displayIndex}";
                key = parts[0].Trim();
            }

            // Skip header rows
            if (key.Equals("KEY", StringComparison.OrdinalIgnoreCase) || 
                key.Equals("API KEY", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("TÊN", StringComparison.OrdinalIgnoreCase) && key.Equals("KEY", StringComparison.OrdinalIgnoreCase))
                continue;

            GeminiApiKeyList.Add(new ApiKeyItem
            {
                Index = displayIndex++,
                Name = name,
                Key = key,
                MaskedKey = MaskApiKey(key)
            });
        }
        GeminiApiKeyCount = GeminiApiKeyList.Count;
    }

    private static string MaskApiKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (key.Length <= 10) return new string('*', key.Length);
        return key[..4] + "****" + key[^4..];
    }

    [RelayCommand]
    private void ImportApiKeyCsv()
    {
        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Title = "Chọn file CSV chứa API Keys",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var newLines = File.ReadAllLines(dialog.FileName)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToList();

                var existingLines = GeminiApiKey.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToList();

                var existingKeys = existingLines.Select(line =>
                {
                    var parts = line.Split(['\t', ';', ','], StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length >= 2 ? parts[1].Trim() : parts[0].Trim();
                }).ToHashSet();

                int addedCount = 0;
                foreach (var newLine in newLines)
                {
                    // Basic header check
                    if (newLine.Contains("TÊN", StringComparison.OrdinalIgnoreCase) && newLine.Contains("KEY", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (newLine.Equals("Tên,Key", StringComparison.OrdinalIgnoreCase) || newLine.Equals("Name,Key", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var parts = newLine.Split(['\t', ';', ','], StringSplitOptions.RemoveEmptyEntries);
                    var key = parts.Length >= 2 ? parts[1].Trim() : parts[0].Trim();

                    if (!existingKeys.Contains(key))
                    {
                        existingLines.Add(newLine);
                        existingKeys.Add(key);
                        addedCount++;
                    }
                }

                GeminiApiKey = string.Join("\n", existingLines);
                UpdateApiKeyList();
                SaveGeminiApiKey();
                ShowToast($"Đã thêm {addedCount} key mới (tổng: {GeminiApiKeyCount})", true);
            }
            catch (Exception ex)
            {
                ShowToast($"Lỗi đọc file CSV: {ex.Message}", false);
            }
        }
    }

    [RelayCommand]
    private void ClearAllApiKeys()
    {
        GeminiApiKey = string.Empty;
        UpdateApiKeyList();
        SaveGeminiApiKey();
        ShowToast("Đã xóa tất cả API key!", true);
    }

    [RelayCommand]
    private void SaveGeminiModel()
    {
        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement");
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            File.WriteAllText(Path.Combine(appDataPath, "gemini_model.txt"), SelectedGeminiModel);
            ShowToast($"Đã chọn model: {SelectedGeminiModel}", true);
        }
        catch { }
    }

    private void LoadGeminiModel()
    {
        try
        {
            var modelFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement", "gemini_model.txt");
            if (File.Exists(modelFile))
            {
                var model = File.ReadAllText(modelFile).Trim();
                if (GeminiModels.Contains(model))
                    SelectedGeminiModel = model;
            }
        }
        catch { }
    }

    [RelayCommand]
    private void SaveGeminiApiKey()
    {
        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement");
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            File.WriteAllText(Path.Combine(appDataPath, "gemini_key.txt"), ProtectedConfig.Protect(GeminiApiKey));
            _AIService.SetApiKeys(GeminiApiKey);
            ShowToast("Đã lưu API Key!", true);
        }
        catch (Exception ex)
        {
            ShowToast($"Lỗi lưu API Key: {ex.Message}", false);
        }
    }

    private void LoadGeminiApiKey()
    {
        try
        {
            var keyFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement", "gemini_key.txt");
            if (File.Exists(keyFile))
                GeminiApiKey = ProtectedConfig.Unprotect(File.ReadAllText(keyFile).Trim());
        }
        catch { }
    }

    [RelayCommand]
    private async Task BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Chọn thư mục lưu hồ sơ",
            InitialDirectory = BaseFolderPath,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            BaseFolderPath = dialog.FolderName;
            SaveSettings();
            if (_onFolderChanged != null)
                await _onFolderChanged();
        }
    }

    public void SaveSettings()
    {
        try
        {
            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement");
            if (!Directory.Exists(settingsPath))
                Directory.CreateDirectory(settingsPath);

            var settingsFile = Path.Combine(settingsPath, "settings.txt");
            File.WriteAllText(settingsFile, $"BaseFolderPath={BaseFolderPath}\nIsDarkTheme={IsDarkTheme}");
            _onSettingsChanged?.Invoke();
        }
        catch { }
    }

    public void LoadSettings()
    {
        try
        {
            var settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocumentManagement", "settings.txt");
            if (File.Exists(settingsFile))
            {
                var lines = File.ReadAllLines(settingsFile);
                foreach (var line in lines)
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        if (parts[0] == "BaseFolderPath" && !string.IsNullOrEmpty(parts[1]))
                            BaseFolderPath = parts[1];
                        else if (parts[0] == "IsDarkTheme" && bool.TryParse(parts[1], out bool isDark))
                        {
                            IsDarkTheme = isDark;
                            ApplyTheme();
                        }
                    }
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }

    [RelayCommand]
    private void ToggleUserMenu()
    {
        IsUserMenuOpen = !IsUserMenuOpen;
    }

    [RelayCommand]
    private void SetThemeCommand(string theme)
    {
        IsDarkTheme = theme == "Dark";
        ApplyTheme();
        SaveSettings();
        IsSettingsOpen = false;
    }
}
