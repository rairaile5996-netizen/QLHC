using System.Runtime.InteropServices;

namespace DocumentManagement.Utilities;

public static class OfficeInteropSession
{
    private const int SW_HIDE = 0;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static dynamic? CreateWordApplication()
    {
        var app = CreateApplication("Word.Application");
        ConfigureWord(app);
        return app;
    }

    public static dynamic? CreateExcelApplication(bool disableInteraction = true)
    {
        var app = CreateApplication("Excel.Application");
        ConfigureExcel(app, disableInteraction);
        return app;
    }

    public static void ConfigureWord(dynamic? wordApp)
    {
        if (wordApp == null) return;

        try { wordApp.Visible = false; } catch { }
        try { wordApp.DisplayAlerts = false; } catch { }
        try { wordApp.ScreenUpdating = false; } catch { }
        try { wordApp.WindowState = 2; } catch { }
        try { wordApp.Left = -10000; } catch { }
        try { wordApp.ShowWindowsInTaskbar = false; } catch { }
        HideWindow(wordApp);
    }

    public static void ConfigureExcel(dynamic? excelApp, bool disableInteraction = true)
    {
        if (excelApp == null) return;

        try { excelApp.Visible = false; } catch { }
        try { excelApp.DisplayAlerts = false; } catch { }
        try { excelApp.ScreenUpdating = false; } catch { }
        try { excelApp.UserControl = false; } catch { }
        try { excelApp.ShowWindowsInTaskbar = false; } catch { }
        if (disableInteraction)
        {
            try { excelApp.EnableEvents = false; } catch { }
            try { excelApp.Interactive = false; } catch { }
        }

        KeepExcelHidden(excelApp);
    }

    public static void KeepExcelHidden(dynamic? excelApp)
    {
        if (excelApp == null) return;

        // Excel's WindowState/Left/Top setters make a hidden application visible.
        // Never minimize or reposition it during clipboard/OLE export.
        try { excelApp.Visible = false; } catch { }
        try { excelApp.ScreenUpdating = false; } catch { }
        try { excelApp.ShowWindowsInTaskbar = false; } catch { }

        try
        {
            var hwnd = new IntPtr((int)excelApp.Hwnd);
            if (hwnd != IntPtr.Zero) ShowWindow(hwnd, SW_HIDE);
        }
        catch { }
    }

    public static void HideWindow(dynamic? app)
    {
        if (app == null) return;

        try { app.Visible = false; } catch { }
        try
        {
            var hwnd = new IntPtr((int)app.Hwnd);
            if (hwnd != IntPtr.Zero) ShowWindow(hwnd, SW_HIDE);
        }
        catch { }
    }

    public static void ReleaseComObject(object? value)
    {
        if (value == null) return;
        try
        {
            if (Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
        catch { }
    }

    public static void QuitAndRelease(dynamic? app, bool restoreExcelInteraction = false)
    {
        if (app == null) return;

        try { app.CutCopyMode = false; } catch { }
        if (restoreExcelInteraction)
        {
            try { app.Interactive = true; } catch { }
            try { app.EnableEvents = true; } catch { }
        }

        try { HideWindow(app); } catch { }
        try { app.Quit(); } catch { }
        ReleaseComObject(app);
    }

    private static dynamic? CreateApplication(string progId)
    {
        var appType = Type.GetTypeFromProgID(progId);
        return appType == null ? null : Activator.CreateInstance(appType);
    }

}
