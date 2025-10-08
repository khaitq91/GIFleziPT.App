namespace GIFleziPT.App.Models;

public class AzureDevOpsTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public string? ParentTitle { get; set; }
    public string? AssignedTo { get; set; }
    public string? Description { get; set; }
}
