using System.IO;
using System.Reflection;
using ClosedXML.Excel;
using DocumentManagement.Services;

namespace DocumentManagement.Tests.Properties;

public class AIServiceExcelProperties
{
    [Fact]
    public void ReadExcelSheet_uses_print_area_and_ignores_values_outside_it()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"ai-print-area-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("Gia tri thanh toan");
                sheet.Cell("A1").Value = "Gia tri thanh quyet toan";
                sheet.Cell("B1").Value = 123_456_789m;
                sheet.Cell("J1").Value = "GIA TRI NGOAI VUNG IN";
                sheet.Cell("K1").Value = 987_654_321m;
                sheet.PageSetup.PrintAreas.Add("A1:C3");
                workbook.SaveAs(filePath);
            }

            var content = InvokeReadExcelSheet(filePath);

            Assert.Contains("123456789", content);
            Assert.DoesNotContain("GIA TRI NGOAI VUNG IN", content);
            Assert.DoesNotContain("987654321", content);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    private static string InvokeReadExcelSheet(string filePath)
    {
        var method = typeof(AIService).GetMethod("ReadExcelSheet", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<string>(method!.Invoke(null, [filePath]));
    }
}
