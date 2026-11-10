namespace DocumentManagement.Services;

public class AppUpdateManifest
{
    public string Version { get; set; } = string.Empty;
    public string PackageUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public List<UpdateChange> Changes { get; set; } = [];
    public string EntryExecutable { get; set; } = "DocumentManagement.exe";
}
