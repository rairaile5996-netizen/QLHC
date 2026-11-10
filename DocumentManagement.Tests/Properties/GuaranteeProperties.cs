using DocumentManagement.Models;
using DocumentManagement.Services;
using System.IO;

namespace DocumentManagement.Tests.Properties;

public class GuaranteeProperties
{
    [Fact]
    public void AlertBoundariesAndResolvedStatesAreStable()
    {
        var today = new DateTime(2026, 7, 15);

        Assert.Equal(GuaranteeAlertSeverity.Warning,
            GuaranteeStatusCalculator.GetAlertSeverity(today.AddDays(30), GuaranteeLifecycleStatus.Active, today));
        Assert.Equal(GuaranteeAlertSeverity.Warning,
            GuaranteeStatusCalculator.GetAlertSeverity(today.AddDays(8), GuaranteeLifecycleStatus.Active, today));
        Assert.Equal(GuaranteeAlertSeverity.Urgent,
            GuaranteeStatusCalculator.GetAlertSeverity(today.AddDays(7), GuaranteeLifecycleStatus.Active, today));
        Assert.Equal(GuaranteeAlertSeverity.Urgent,
            GuaranteeStatusCalculator.GetAlertSeverity(today, GuaranteeLifecycleStatus.Active, today));
        Assert.Equal(GuaranteeAlertSeverity.Overdue,
            GuaranteeStatusCalculator.GetAlertSeverity(today.AddDays(-1), GuaranteeLifecycleStatus.Active, today));
        Assert.Equal(GuaranteeAlertSeverity.None,
            GuaranteeStatusCalculator.GetAlertSeverity(today.AddDays(-1), GuaranteeLifecycleStatus.Replaced, today));
        Assert.Equal(GuaranteeAlertSeverity.None,
            GuaranteeStatusCalculator.GetAlertSeverity(today.AddDays(3), GuaranteeLifecycleStatus.Released, today));
    }

    [Fact]
    public async Task CopyPdfCreatesSeparateFilesForDuplicateNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "DocumentManagementGuaranteeTests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source.pdf");
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(source, [0x25, 0x50, 0x44, 0x46]);

        try
        {
            var service = new GuaranteeFileService();
            var first = await service.CopyPdfAsync(source, root, null, "Hợp đồng gốc");
            var second = await service.CopyPdfAsync(source, root, null, "Hợp đồng gốc");

            Assert.NotEqual(first, second);
            Assert.True(File.Exists(first));
            Assert.True(File.Exists(second));
            Assert.EndsWith(Path.Combine("4.HSTU", "HD GOC"), Path.GetDirectoryName(first)!);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(GuaranteeType.AdvancePayment, "4.HSTU")]
    [InlineData(GuaranteeType.Warranty, "7.KTBH")]
    [InlineData(GuaranteeType.Performance, "1.INPUT")]
    public void DestinationFolderUsesGuaranteeTypeRoot(GuaranteeType type, string expectedRoot)
    {
        var service = new GuaranteeFileService();
        var path = service.GetDestinationFolder("D:\\HỒ SƠ", null, "Hợp đồng gốc", type);

        Assert.Contains(expectedRoot, path, StringComparison.OrdinalIgnoreCase);
    }
}
