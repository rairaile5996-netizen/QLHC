using DocumentManagement.Services;

namespace DocumentManagement.Tests.Properties;

public class UpdateVersionComparerProperties
{
    [Theory]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.1.0", "1.0.9", false)]
    [InlineData("1.0.0", "1.0.0-beta", false)]
    public void IsNewer_compares_versions_safely(string currentVersion, string latestVersion, bool expected)
    {
        var actual = UpdateVersionComparer.IsNewer(currentVersion, latestVersion);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("https://example.com/update-manifest.json", true)]
    [InlineData("file:///C:/temp/update-manifest.json", true)]
    [InlineData("PUT_YOUR_UPDATE_MANIFEST_URL_HERE", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsConfiguredValue_detects_placeholder_values(string value, bool expected)
    {
        var actual = UpdateVersionComparer.IsConfiguredValue(value);
        Assert.Equal(expected, actual);
    }
}
