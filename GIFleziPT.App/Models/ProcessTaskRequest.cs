namespace GIFleziPT.App.Models;

public class ProcessTaskRequest
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string TaskDescription { get; set; } = string.Empty;
}
