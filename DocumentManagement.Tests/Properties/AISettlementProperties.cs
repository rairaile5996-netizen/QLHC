using System.IO;
using System.Reflection;
using ClosedXML.Excel;
using DocumentManagement.Services;

namespace DocumentManagement.Tests.Properties;

public class AISettlementProperties
{
    [Fact]
    public void Reader_includes_late_rows_wide_columns_and_other_sheets_inside_print_areas()
    {
        var path = Path.Combine(Path.GetTempPath(), $"settlement-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var book = new XLWorkbook())
            {
                book.AddWorksheet("Giá trị thanh toán").Cell("A1").Value = "Thanh toán đợt cuối";
                var summary = book.AddWorksheet("Tổng hợp quyết toán");
                summary.Cell("A45").Value = "Giá trị quyết toán";
                summary.Cell("J45").Value = 1_000_000_000m;
                summary.Cell("K45").Value = "NGOAI VUNG IN";
                summary.PageSetup.PrintAreas.Add("A1:J50");
                book.SaveAs(path);
            }
            var text = Assert.IsType<string>(Invoke("ReadSettlementSheet", path));
            Assert.Contains("Thanh toán đợt cuối", text);
            Assert.Contains("Giá trị quyết toán", text);
            Assert.Contains("1000000000", text);
            Assert.DoesNotContain("NGOAI VUNG IN", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Explicit_zero_warranty_uses_five_percent_of_settlement_not_payment()
    {
        var result = Parse("""{"isSettlement":true,"settlementValue":1000000000,"remainingPayment":200000000,"warrantyValue":0}""");
        Assert.Equal(1_000_000_000m, result.SettlementValue);
        Assert.Equal(200_000_000m, result.RemainingPayment);
        Assert.Equal(50_000_000m, result.WarrantyValue);
        Assert.True(result.WarrantyValueWasZero);
    }

    [Fact]
    public void Explicit_warranty_and_zero_payment_are_preserved()
    {
        var result = Parse("""{"isSettlement":true,"settlementValue":1000000000,"remainingPayment":0,"warrantyValue":30000000}""");
        Assert.Equal(0m, result.RemainingPayment);
        Assert.Equal(30_000_000m, result.WarrantyValue);
        Assert.False(result.WarrantyValueWasZero);
    }

    [Theory]
    [InlineData("settlementValue")]
    [InlineData("remainingPayment")]
    [InlineData("warrantyValue")]
    public void Unknown_amount_does_not_silently_become_zero(string field)
    {
        var json = """{"isSettlement":true,"settlementValue":1000000000,"remainingPayment":200000000,"warrantyValue":10000000}""";
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var values = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
        values[field] = null;
        var error = Assert.Throws<TargetInvocationException>(() => Parse(System.Text.Json.JsonSerializer.Serialize(values)));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    private static AIService.SettlementExtractResult Parse(string json) =>
        Assert.IsType<AIService.SettlementExtractResult>(Invoke("ParseSettlementResult", json));

    private static object? Invoke(string method, string argument) =>
        typeof(AIService).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [argument]);
}
