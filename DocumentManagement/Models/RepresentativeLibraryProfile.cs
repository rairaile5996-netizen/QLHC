namespace DocumentManagement.Models;

/// <summary>
/// A reusable representative list. Profiles are intentionally stored outside
/// contracts so loading one always creates an independent contract snapshot.
/// </summary>
public sealed class RepresentativeLibraryProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PartyKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public string DefaultSignerName { get; set; } = string.Empty;
    public string DefaultSignerPosition { get; set; } = string.Empty;
    public List<RepresentativeLibraryMember> Members { get; set; } = [];
}

public sealed class RepresentativeLibraryMember
{
    public string FullName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
}
