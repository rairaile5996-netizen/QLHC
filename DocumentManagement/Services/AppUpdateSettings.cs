namespace DocumentManagement.Services;

public class AppUpdateSettings
{
    public string ManifestUrl { get; set; } = string.Empty;
    public bool CheckOnStartup { get; set; } = true;
    public string Channel { get; set; } = "stable";
    public bool SimulateDownload { get; set; }
}
