namespace DocumentManagement.Models;

public class AIFileItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class AppendixFileItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public enum DeleteOption
{
    Cancel,
    DeleteAll,
    DeleteAppOnly
}
