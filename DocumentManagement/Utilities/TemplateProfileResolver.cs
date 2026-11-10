using DocumentManagement.Models;
using System.IO;
using System.Linq;

namespace DocumentManagement.Utilities;

public static class TemplateProfileResolver
{
    public static string GetTemplateRoot(CvxdGlobalConfig config, string? profile = null)
    {
        var root = GetTemplateRootFolder(config);
        var selected = ResolveProfile(root, profile);
        var modern = Path.Combine(selected, "HSCL", "WordTemplates");
        if (Directory.Exists(modern)) return modern;
        var profileWordTemplates = Path.Combine(selected, "WordTemplates");
        if (!string.Equals(selected, root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(profileWordTemplates))
            return profileWordTemplates;

        var legacy = Path.Combine(root, "WordTemplates", profile?.Trim() ?? string.Empty);
        if (Directory.Exists(legacy)) return legacy;
        return Directory.Exists(Path.Combine(root, "WordTemplates"))
            ? Path.Combine(root, "WordTemplates")
            : modern;
    }

    public static string GetChecklistRoot(CvxdGlobalConfig config, string? profile = null)
    {
        var root = GetTemplateRootFolder(config);
        var selected = ResolveProfile(root, profile);
        if (string.IsNullOrWhiteSpace(profile))
        {
            var rootChecklist = Path.Combine(root, "ChecklistTemplates");
            return rootChecklist;
        }
        var modern = Path.Combine(selected, "HSCL", "ChecklistTemplates");
        if (Directory.Exists(modern)) return modern;

        var legacy = Path.Combine(root, "ChecklistTemplates", profile?.Trim() ?? string.Empty);
        if (Directory.Exists(legacy)) return legacy;
        return Directory.Exists(Path.Combine(root, "ChecklistTemplates"))
            ? Path.Combine(root, "ChecklistTemplates")
            : modern;
    }

    public static string GetHsplRoot(CvxdGlobalConfig config, string? profile = null)
    {
        var root = GetTemplateRootFolder(config);
        var selected = ResolveProfile(root, profile);
        var modern = Path.Combine(selected, "HSPL");
        if (Directory.Exists(modern)) return modern;

        var legacy = Path.Combine(root, "HSPL_templates", profile?.Trim() ?? string.Empty);
        if (Directory.Exists(legacy)) return legacy;
        return Directory.Exists(Path.Combine(root, "HSPL_templates"))
            ? Path.Combine(root, "HSPL_templates")
            : modern;
    }

    public static string GetOtherRoot(CvxdGlobalConfig config, string? profile = null)
    {
        var root = GetTemplateRootFolder(config);
        var selected = ResolveProfile(root, profile);
        var modern = Path.Combine(selected, "HSCL", "HoSoKhac");
        if (Directory.Exists(modern)) return modern;

        var legacy = Path.Combine(root, "HoSoKhac", profile?.Trim() ?? string.Empty);
        if (Directory.Exists(legacy)) return legacy;
        return modern;
    }

    public static IReadOnlyList<string> GetProfiles(CvxdGlobalConfig config)
    {
        var root = GetProfilesRoot(config);
        var profiles = new List<string>();
        if (Directory.Exists(root))
        {
            var structuralFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "HSCL", "HSPL", "WordTemplates", "ChecklistTemplates", "HSPL_templates",
                "Checklists", "HoSoKhac", "VTĐV", "VTDV"
            };
            profiles.AddRange(Directory.GetDirectories(root)
                .Where(path => !structuralFolders.Contains(Path.GetFileName(path)))
                .Where(path => new[] { "HSCL", "HSPL", "WordTemplates", "ChecklistTemplates", "HSPL_templates", "Checklists" }
                    .Any(folder => Directory.Exists(Path.Combine(path, folder))))
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!));

            foreach (var legacyFolder in new[] { "WordTemplates", "ChecklistTemplates", "HSPL_templates" })
            {
                var path = Path.Combine(root, legacyFolder);
                if (Directory.Exists(path))
                    profiles.AddRange(Directory.GetDirectories(path).Select(Path.GetFileName).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!));
            }
        }

        return profiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static string GetProfilesRoot(CvxdGlobalConfig config) => GetTemplateRootFolder(config);

    public static string GetVtdvFolder(string templateRoot)
    {
        var candidates = new[] { "VTĐV", "VTDV" };
        return candidates.Select(name => Path.Combine(templateRoot, name)).FirstOrDefault(Directory.Exists)
            ?? Path.Combine(templateRoot, "VTĐV");
    }

    private static string GetTemplateRootFolder(CvxdGlobalConfig config)
    {
        var configured = FirstExisting(config.TemplateFolderPath, config.ChecklistFolderPath, config.HsplTemplateFolderPath);
        if (string.IsNullOrWhiteSpace(configured))
            return Path.Combine(AppContext.BaseDirectory, "Templates");

        var leaf = Path.GetFileName(configured.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (leaf.Equals("WordTemplates", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("ChecklistTemplates", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("HSPL_templates", StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetParent(configured)?.FullName ?? configured;
        }

        return configured;
    }

    private static string ResolveProfile(string root, string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile)) return root;
        var profilePath = Path.Combine(root, profile.Trim());
        return Directory.Exists(profilePath) ? profilePath : root;
    }

    private static string? FirstExisting(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && Directory.Exists(value));
    }
}
