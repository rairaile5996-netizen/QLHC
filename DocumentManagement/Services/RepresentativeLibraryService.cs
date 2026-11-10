using System.Text.Json;
using System.IO;
using DocumentManagement.Models;

namespace DocumentManagement.Services;

/// <summary>Persists reusable party representative lists for all contracts.</summary>
public sealed class RepresentativeLibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DocumentManagement",
        "RepresentativeLibraries.json");

    public List<RepresentativeLibraryProfile> Load()
    {
        if (!File.Exists(LibraryPath)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<RepresentativeLibraryProfile>>(
                File.ReadAllText(LibraryPath)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<RepresentativeLibraryProfile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath)!);
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(profiles, JsonOptions));
    }
}
