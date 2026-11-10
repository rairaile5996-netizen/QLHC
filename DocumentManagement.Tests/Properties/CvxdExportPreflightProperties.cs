using DocumentManagement.Models;
using DocumentManagement.Services;
using System.IO;

namespace DocumentManagement.Tests.Properties;

public class CvxdExportPreflightProperties
{
    [Fact]
    public void ValidateExport_reports_error_when_no_rows_are_selected()
    {
        var service = new CvxdExportService();
        var result = service.ValidateExport(new CvxdGlobalConfig(), [new CvxdItem { IsSelected = false }], exportAll: false);

        Assert.False(result.CanExport);
        Assert.Contains(result.Errors, e => e.Contains("tích chọn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateExport_reports_missing_template()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "cvxd_preflight_" + Guid.NewGuid());
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new CvxdExportService();
            var config = new CvxdGlobalConfig { TemplateFolderPath = tempRoot };
            var result = service.ValidateExport(config, [new CvxdItem { IsSelected = true, LoaiNt = "CVXD", SoChecklist = "0" }], exportAll: false);

            Assert.False(result.CanExport);
            Assert.Contains(result.Errors, e => e.Contains("Thiếu template", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ValidateExport_uses_separate_templates_for_cvxd_and_ldttb()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "cvxd_template_split_" + Guid.NewGuid());
        var templateRoot = Path.Combine(tempRoot, "WordTemplates");
        var cvxdFolder = Path.Combine(templateRoot, "CVXD");
        Directory.CreateDirectory(cvxdFolder);
        var cvxdTemplate = Path.Combine(cvxdFolder, "CVXD_Template.docx");
        var ldttbTemplate = Path.Combine(cvxdFolder, "LĐTTB_Template.docx");
        File.WriteAllText(cvxdTemplate, "CVXD template");
        File.WriteAllText(ldttbTemplate, "LĐTTB template");

        try
        {
            var service = new CvxdExportService();
            var config = new CvxdGlobalConfig { TemplateFolderPath = tempRoot };
            var cvxdItem = new CvxdItem { IsSelected = true, LoaiNt = "CVXD", SoChecklist = "0" };
            var ldttbItem = new CvxdItem { IsSelected = true, LoaiNt = "LĐTTB", SoChecklist = "0" };

            Assert.True(service.ValidateExport(config, [cvxdItem], exportAll: false).CanExport);
            Assert.True(service.ValidateExport(config, [ldttbItem], exportAll: false).CanExport);

            File.Delete(ldttbTemplate);
            Assert.True(service.ValidateExport(config, [cvxdItem], exportAll: false).CanExport);
            var missingLdttb = service.ValidateExport(config, [ldttbItem], exportAll: false);
            Assert.False(missingLdttb.CanExport);
            Assert.Contains(missingLdttb.Errors, error => error.Contains("LĐTTB_Template.docx", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
