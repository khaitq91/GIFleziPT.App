using GIFleziPT.App.Models;

namespace GIFleziPT.App.Services;

public interface ITaskService
{
    Task<ProcessTaskResult> ProcessTaskAsync(ProcessTaskRequest request);
    Task<GetAzureDevOpsTasksResult> GetAzureDevOpsTasksAsync();
    Task RunAsync();
    Task<AzureDevOpsTask> UpdateAzureDevOpsTaskStateAsync(int taskId, string newState);
}