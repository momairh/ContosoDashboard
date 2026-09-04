namespace ContosoDashboard.Models;

public class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    public string RootPath { get; set; } = "AppData/uploads";

    public long MaxFileSizeBytes { get; set; } = 26_214_400;

    public List<string> AllowedExtensions { get; set; } = new();
}
