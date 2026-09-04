namespace ContosoDashboard.Models;

public static class DocumentCategory
{
    public const string ProjectDocumentation = "Project Documentation";
    public const string Reports = "Reports";
    public const string Presentations = "Presentations";
    public const string Contracts = "Contracts";
    public const string Templates = "Templates";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ProjectDocumentation,
        Reports,
        Presentations,
        Contracts,
        Templates,
        Other
    };

    public static bool IsValid(string? category) =>
        !string.IsNullOrWhiteSpace(category) && All.Contains(category);
}
