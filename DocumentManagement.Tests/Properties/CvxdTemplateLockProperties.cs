using DocumentManagement.Models;

namespace DocumentManagement.Tests.Properties;

public class CvxdTemplateLockProperties
{
    [Theory]
    [InlineData("PKH", false, true)]
    [InlineData("pkh", false, true)]
    [InlineData("BÀ NÀ", true, false)]
    [InlineData("BA NA", true, false)]
    [InlineData("Tổng thầu", true, true)]
    public void Template_profile_locks_only_its_non_applicable_party(
        string profile,
        bool tvgsEnabled,
        bool employerEnabled)
    {
        var info = new CvxdInfo { TemplateProfile = profile };

        Assert.Equal(tvgsEnabled, info.IsTvgsSectionEnabled);
        Assert.Equal(!tvgsEnabled, info.IsTvgsSectionLocked);
        Assert.Equal(employerEnabled, info.IsEmployerSectionEnabled);
        Assert.Equal(!employerEnabled, info.IsEmployerSectionLocked);
    }

    [Fact]
    public void Changing_template_profile_updates_party_lock_properties()
    {
        var info = new CvxdInfo { TemplateProfile = "PKH" };

        info.TemplateProfile = "Tổng thầu";

        Assert.True(info.IsTvgsSectionEnabled);
        Assert.True(info.IsEmployerSectionEnabled);
    }
}
