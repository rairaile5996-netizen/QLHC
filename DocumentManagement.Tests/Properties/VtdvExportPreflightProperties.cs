using System.Collections.ObjectModel;
using System.IO;
using DocumentManagement.Models;
using DocumentManagement.Services;

namespace DocumentManagement.Tests.Properties;

public class VtdvExportPreflightProperties
{
    [Fact]
    public void ValidateExport_reports_error_when_no_sessions_are_selected()
    {
        var service = new VtdvExportService();
        var result = service.ValidateExport(new CvxdGlobalConfig(), [], templatePath: "dummy.docx");

        Assert.False(result.CanExport);
        Assert.Contains(result.Errors, e => e.Contains("ít nhất một biên bản VTĐV", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateExport_reports_missing_checklist_template()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "vtdv_preflight_" + Guid.NewGuid());
        Directory.CreateDirectory(tempRoot);
        var templatePath = Path.Combine(tempRoot, "template.docx");
        File.WriteAllText(templatePath, "stub");

        try
        {
            var session = new VtdvSession
            {
                SoBienBan = "BB-01",
                NgayNghiệmThu = "08/07/2026",
                DoiTuongNghiệmThu = "Vật tư đầu vào",
                Items =
                [
                    new VtdvItem
                    {
                        DoiTuong = "Ống thép",
                        DonVi = "m",
                        SoLuong = "12",
                        ThuongHieu = "Hoa Phat"
                    }
                ]
            };

            var service = new VtdvExportService();
            var config = new CvxdGlobalConfig { TemplateFolderPath = tempRoot };
            var result = service.ValidateExport(config, [session], templatePath);

            Assert.False(result.CanExport);
            Assert.Contains(result.Errors, e => e.Contains("checklist_template.xlsx", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
