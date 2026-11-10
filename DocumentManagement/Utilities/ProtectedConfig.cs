using System.Security.Cryptography;
using System.Text;

namespace DocumentManagement.Utilities;

public static class ProtectedConfig
{
    private const string Prefix = "DPAPI:";

    public static string Protect(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (!value.StartsWith(Prefix, StringComparison.Ordinal)) return value;

        var payload = value[Prefix.Length..];
        var protectedBytes = Convert.FromBase64String(payload);
        var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
