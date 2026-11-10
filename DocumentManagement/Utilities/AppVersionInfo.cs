using System.Reflection;

namespace DocumentManagement.Utilities;

public static class AppVersionInfo
{
    public static string CurrentVersion
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                var plusIndex = informationalVersion.IndexOf('+');
                return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
            }

            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }
    }

    public static string VersionLabel => $"Phiên bản {CurrentVersion}";

    public static string ProductVersionLabel => $"Hồ Sơ Xây Dựng Pro v{CurrentVersion}";
}
