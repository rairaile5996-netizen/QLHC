namespace DocumentManagement.Models;

public class ApiKeyItem
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string MaskedKey { get; set; } = string.Empty;
}
