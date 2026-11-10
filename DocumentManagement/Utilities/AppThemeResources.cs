using System.IO;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace DocumentManagement.Utilities;

public static class AppThemeResources
{
    public static bool LoadSavedIsDarkTheme()
    {
        try
        {
            var settingsFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DocumentManagement",
                "settings.txt");

            if (!File.Exists(settingsFile))
            {
                return false;
            }

            foreach (var line in File.ReadAllLines(settingsFile))
            {
                var parts = line.Split('=', 2);

                if (parts.Length == 2 &&
                    parts[0] == "IsDarkTheme" &&
                    bool.TryParse(parts[1], out var isDark))
                {
                    return isDark;
                }
            }
        }
        catch
        {
            // Keep theme loading non-blocking.
        }

        return false;
    }

    public static void Apply(bool isDark)
    {
        var app = Application.Current;
        if (app == null || app.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(new Action(() => Apply(isDark)));
            return;
        }

        var resources = app.Resources;

        Apply(resources, isDark);
        ApplyToOpenWindows(isDark);

        ConfirmDialog.SetTheme(isDark);
    }

    public static void Apply(ResourceDictionary resources, bool isDark)
    {
        // =========================================================
        // SURFACE
        // =========================================================

        Set(resources, "Blueprint.Surface",
            isDark ? "#0F172A" : "#EAF0F6");

        Set(resources, "Blueprint.SurfaceContainer",
            isDark ? "#070A13" : "#F7FAFC");

        Set(resources, "Blueprint.SurfaceBright",
            isDark ? "#1E293B" : "#E1E8F0");

        Set(resources, "Blueprint.SurfaceContainerHigh",
            isDark ? "#27344D" : "#D7E1EC");

        Set(resources, "Blueprint.OnSurface",
            isDark ? "#FFFFFF" : "#172033");

        Set(resources, "Blueprint.OnSurfaceVariant",
            isDark ? "#94A3B8" : "#4B5F78");

        Set(resources, "Blueprint.Outline",
            isDark ? "#334155" : "#BAC7D6");


        // =========================================================
        // PRIMARY
        // =========================================================

        Set(resources, "Blueprint.Primary", "#2563EB");
        Set(resources, "Blueprint.OnPrimary", "#FFFFFF");
        Set(resources, "Blueprint.ModuleContractContainer", isDark ? "#172E55" : "#DBEAFE");
        Set(resources, "Blueprint.ModuleAcceptanceAccent", isDark ? "#A78BFA" : "#6D28D9");
        Set(resources, "Blueprint.ModuleAcceptanceContainer", isDark ? "#2E2545" : "#EDE9FE");

        // Solid interaction colors keep labels readable in both themes.
        Set(resources, "Blueprint.ButtonHover", isDark ? "#334155" : "#CCD7E4");
        Set(resources, "Blueprint.ButtonPressed", isDark ? "#475569" : "#BBCBDD");
        Set(resources, "Blueprint.PrimaryHover", "#1D4ED8");
        Set(resources, "Blueprint.PrimaryPressed", "#1E40AF");
        Set(resources, "Blueprint.Danger", isDark ? "#FB7185" : "#BE123C");
        Set(resources, "Blueprint.DangerBackground", isDark ? "#301B2B" : "#FDE7EC");
        Set(resources, "Blueprint.DangerHover", isDark ? "#422032" : "#FBD5DF");
        Set(resources, "Blueprint.DangerPressed", isDark ? "#53243A" : "#F9C4D2");
        Set(resources, "Blueprint.SuccessHover", isDark ? "#115E59" : "#A7F3D0");
        Set(resources, "Blueprint.SuccessPressed", isDark ? "#134E4A" : "#6EE7B7");
        Set(resources, "Blueprint.SuccessSolidHover", "#047857");
        Set(resources, "Blueprint.SuccessSolidPressed", "#065F46");
        Set(resources, "Blueprint.WarningHover", "#B45309");
        Set(resources, "Blueprint.WarningPressed", "#92400E");

        Set(resources, "Blueprint.PrimaryContainer",
            isDark ? "#1E293B" : "#DBEAFE");

        Set(resources, "Blueprint.OnPrimaryContainer",
            isDark ? "#BFDBFE" : "#1E40AF");


        // =========================================================
        // ERROR
        // =========================================================

        Set(resources, "Blueprint.Error",
            isDark ? "#F87171" : "#DC2626");

        Set(resources, "Blueprint.ErrorContainer",
            isDark ? "#450A0A" : "#FEE2E2");

        Set(resources, "Blueprint.OnErrorContainer",
            isDark ? "#FECACA" : "#991B1B");


        // =========================================================
        // WARNING
        // =========================================================

        Set(resources, "Blueprint.Warning",
            isDark ? "#FBBF24" : "#D97706");

        Set(resources, "Blueprint.WarningContainer",
            isDark ? "#4D2C00" : "#FEF3C7");

        Set(resources, "Blueprint.OnWarningContainer",
            isDark ? "#FDE68A" : "#92400E");


        // =========================================================
        // SUCCESS
        // =========================================================

        Set(resources, "Blueprint.SuccessContainer",
            isDark ? "#0F766E" : "#D1FAE5");

        Set(resources, "Blueprint.SuccessOutline",
            isDark ? "#5EEAD4" : "#10B981");

        Set(resources, "Blueprint.OnSuccessContainer",
            isDark ? "#E6FDF9" : "#065F46");


        // =========================================================
        // INFO
        // =========================================================

        Set(resources, "Blueprint.InfoContainer",
            isDark ? "#082F49" : "#E0F2FE");

        Set(resources, "Blueprint.OnInfoContainer",
            isDark ? "#BAE6FD" : "#075985");


        // =========================================================
        // STATUS
        // =========================================================

        Set(resources, "Status.InProgress",
            isDark ? "#60A5FA" : "#2563EB");

        Set(resources, "Status.Completed",
            isDark ? "#34D399" : "#059669");

        Set(resources, "Status.Pending",
            isDark ? "#FBBF24" : "#D97706");

        Set(resources, "Status.Error",
            isDark ? "#F87171" : "#DC2626");


        // =========================================================
        // SELECTION / FOCUS
        // =========================================================

        Set(resources, "Blueprint.Selection",
            isDark ? "#334155" : "#D7E7FB");

        Set(resources, "Blueprint.SelectionStrong",
            isDark ? "#2563EB" : "#BFDBFE");

        Set(resources, "Blueprint.Focus",
            isDark ? "#22D3EE" : "#2563EB");


        // =========================================================
        // SCROLL / OVERLAY
        // =========================================================

        Set(resources, "Blueprint.ScrollThumb",
            isDark ? "#40000000" : "#667C8FA8");

        Set(resources, "Blueprint.ScrollThumbHover",
            isDark ? "#60000000" : "#995B6B80");

        Set(resources, "Blueprint.BusyOverlay",
            isDark ? "#CC051424" : "#B8F1F5F9");


        // =========================================================
        // MATERIAL DESIGN BASE THEME
        // =========================================================

        try
        {
            var paletteHelper = new PaletteHelper();

            var theme = paletteHelper.GetTheme();

            theme.SetBaseTheme(
                isDark
                    ? BaseTheme.Dark
                    : BaseTheme.Light);

            paletteHelper.SetTheme(theme);
        }
        catch
        {
            // Suppress errors if MaterialDesign resources
            // aren't fully loaded.
        }
    }

    private static void ApplyToOpenWindows(bool isDark)
    {
        var windows = Application.Current?.Windows;

        if (windows == null)
        {
            return;
        }

        foreach (Window window in windows)
        {
            try
            {
                Apply(window.Resources, isDark);
            }
            catch
            {
                // Keep theme switching resilient.
            }
        }
    }

    private static void Set(
        ResourceDictionary resources,
        string key,
        string hex)
    {
        resources[key] = CreateBrush(hex);

        foreach (var dictionary in resources.MergedDictionaries)
        {
            SetMerged(dictionary, key, hex);
        }
    }

    private static void SetMerged(
        ResourceDictionary resources,
        string key,
        string hex)
    {
        if (resources.Contains(key))
        {
            resources[key] = CreateBrush(hex);
        }

        foreach (var dictionary in resources.MergedDictionaries)
        {
            SetMerged(dictionary, key, hex);
        }
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        return new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(hex));
    }
}
