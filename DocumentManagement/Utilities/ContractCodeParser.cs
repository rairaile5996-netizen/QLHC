namespace DocumentManagement.Utilities;

public static class ContractCodeParser
{
    public static (string EmployerCode, string ContractorCode) Extract(string? contractNumber)
    {
        if (string.IsNullOrWhiteSpace(contractNumber)) return (string.Empty, string.Empty);

        var codeSegment = contractNumber.Contains('/')
            ? contractNumber.Split('/').Last()
            : contractNumber;

        if (codeSegment.StartsWith("HĐ-", StringComparison.OrdinalIgnoreCase))
            codeSegment = codeSegment[3..];

        var codes = codeSegment.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return codes.Length >= 2
            ? (codes[0], codes[^1])
            : (string.Empty, string.Empty);
    }
}
