namespace DocumentManagement.Services;

public static class UpdateVersionComparer
{
    public static bool IsNewer(string currentVersion, string latestVersion)
    {
        if (!TryParse(currentVersion, out var current))
        {
            current = new Version(0, 0, 0, 0);
        }

        if (!TryParse(latestVersion, out var latest))
        {
            return false;
        }

        return latest > current;
    }

    public static bool IsConfiguredValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return !normalized.StartsWith("PUT_", StringComparison.OrdinalIgnoreCase) &&
               !normalized.StartsWith("CHANGE_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParse(string? value, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        var plusIndex = normalized.IndexOf('+');
        if (plusIndex >= 0)
        {
            normalized = normalized[..plusIndex];
        }

        var dashIndex = normalized.IndexOf('-');
        if (dashIndex >= 0)
        {
            normalized = normalized[..dashIndex];
        }

        if (!Version.TryParse(normalized, out var parsedVersion))
        {
            return false;
        }

        version = parsedVersion;
        return true;
    }
}
