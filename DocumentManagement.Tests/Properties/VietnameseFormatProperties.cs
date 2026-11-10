using DocumentManagement.Utilities;

namespace DocumentManagement.Tests.Properties;

public class VietnameseFormatProperties
{
    [Theory]
    [InlineData("1.234.567", 1234567)]
    [InlineData("1,234,567", 1234567)]
    [InlineData("1 234 567 đ", 1234567)]
    [InlineData("1234567", 1234567)]
    public void TryParseMoney_accepts_common_currency_formats(string input, decimal expected)
    {
        Assert.True(VietnameseFormat.TryParseMoney(input, out var actual));
        Assert.Equal(expected, actual);
    }
}
