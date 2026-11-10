using System.IO;
using DocumentManagement.Models;
using DocumentManagement.Utilities;

namespace DocumentManagement.Tests.Properties;

public class TemplateProfileResolverTests
{
    [Fact]
    public void Blank_profile_does_not_use_root_level_hscl_as_a_template_profile()
    {
        using var templateRoot = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(templateRoot.Path, "HSCL", "ChecklistTemplates"));

        var config = new CvxdGlobalConfig
        {
            TemplateFolderPath = templateRoot.Path,
            ChecklistFolderPath = templateRoot.Path
        };

        var checklistRoot = TemplateProfileResolver.GetChecklistRoot(config, null);

        Assert.Equal(Path.Combine(templateRoot.Path, "ChecklistTemplates"), checklistRoot);
    }

    [Fact]
    public void Discovers_additional_profile_folders_and_ignores_structural_hscl_folder()
    {
        using var templateRoot = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(templateRoot.Path, "HSCL", "ChecklistTemplates"));
        Directory.CreateDirectory(Path.Combine(templateRoot.Path, "BÀ NÀ", "HSCL"));
        Directory.CreateDirectory(Path.Combine(templateRoot.Path, "PKH", "HSPL"));
        Directory.CreateDirectory(Path.Combine(templateRoot.Path, "TỔNG THẦU", "HSCL"));
        Directory.CreateDirectory(Path.Combine(templateRoot.Path, "Mẫu mới", "WordTemplates"));

        var config = new CvxdGlobalConfig { TemplateFolderPath = templateRoot.Path };

        var profiles = TemplateProfileResolver.GetProfiles(config);

        Assert.Contains("BÀ NÀ", profiles);
        Assert.Contains("PKH", profiles);
        Assert.Contains("TỔNG THẦU", profiles);
        Assert.Contains("Mẫu mới", profiles);
        Assert.DoesNotContain("HSCL", profiles);
        Assert.Equal(
            Path.Combine(templateRoot.Path, "Mẫu mới", "WordTemplates"),
            TemplateProfileResolver.GetTemplateRoot(config, "Mẫu mới"));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"template-profiles-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
