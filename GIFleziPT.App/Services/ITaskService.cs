using GIFleziPT.App.Models;

namespace GIFleziPT.App.Services;

public interface ITaskService
{
    Task<ProcessTaskResult> ProcessTaskAsync(ProcessTaskRequest request);
}