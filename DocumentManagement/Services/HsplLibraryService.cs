using System.Text.Json;
using System.IO;
using DocumentManagement.Models;

namespace DocumentManagement.Services;

/// <summary>Stores reusable company personnel lists separately from per-contract HSPL data.</summary>
public sealed class HsplLibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DocumentManagement",
        "HsplCompanyLibraries.json");
    private static readonly string PersonnelLibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DocumentManagement",
        "HsplPersonnelLibraries.json");
    private static readonly string WorkerLibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DocumentManagement",
        "HsplWorkerLibraries.json");

    public List<HsplLibraryProfile> Load()
    {
        if (!File.Exists(LibraryPath)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<HsplLibraryProfile>>(File.ReadAllText(LibraryPath)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<HsplLibraryProfile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath)!);
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(profiles, JsonOptions));
    }

    public List<HsplPersonnelLibraryProfile> LoadPersonnel()
    {
        var profiles = LoadFile<HsplPersonnelLibraryProfile>(PersonnelLibraryPath);
        return profiles.Count > 0
            ? profiles
            : Load().Where(profile => profile.Personnel.Count > 0)
                .Select(profile => new HsplPersonnelLibraryProfile { Id = profile.Id, Name = profile.Name, Personnel = profile.Personnel })
                .ToList();
    }

    public List<HsplWorkerLibraryProfile> LoadWorkers()
    {
        var profiles = LoadFile<HsplWorkerLibraryProfile>(WorkerLibraryPath);
        return profiles.Count > 0
            ? profiles
            : Load().Where(profile => profile.Workers.Count > 0)
                .Select(profile => new HsplWorkerLibraryProfile { Id = profile.Id, Name = profile.Name, Workers = profile.Workers })
                .ToList();
    }

    public void SavePersonnel(IEnumerable<HsplPersonnelLibraryProfile> profiles) => SaveFile(PersonnelLibraryPath, profiles);

    public void SaveWorkers(IEnumerable<HsplWorkerLibraryProfile> profiles) => SaveFile(WorkerLibraryPath, profiles);

    private static List<T> LoadFile<T>(string path)
    {
        if (!File.Exists(path)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path)) ?? []; }
        catch { return []; }
    }

    private static void SaveFile<T>(string path, IEnumerable<T> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(profiles, JsonOptions));
    }
}
