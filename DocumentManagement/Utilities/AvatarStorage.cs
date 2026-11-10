using System.IO;

namespace DocumentManagement.Utilities;

public static class AvatarStorage
{
    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".webp"];

    public static string CopyToAppData(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return sourcePath;

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            extension = ".png";

        var avatarFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DocumentManagement",
            "Avatars");

        Directory.CreateDirectory(avatarFolder);

        var destinationPath = Path.Combine(avatarFolder, $"{Guid.NewGuid():N}{extension}");
        File.Copy(sourcePath, destinationPath, overwrite: false);
        return destinationPath;
    }
}
