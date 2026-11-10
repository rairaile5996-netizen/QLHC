using DocumentManagement.Utilities;

namespace DocumentManagement.Tests.Properties;

public class ContractCodeParserProperties
{
    [Theory]
    [InlineData("16/2026/HĐ/OLP-KHC", "OLP", "KHC")]
    [InlineData("13/2026/HĐ/BMC-189", "BMC", "189")]
    public void Extract_returns_employer_and_contractor_codes_from_contract_number(
        string contractNumber,
        string expectedEmployerCode,
        string expectedContractorCode)
    {
        var result = ContractCodeParser.Extract(contractNumber);

        Assert.Equal(expectedEmployerCode, result.EmployerCode);
        Assert.Equal(expectedContractorCode, result.ContractorCode);
    }
}
