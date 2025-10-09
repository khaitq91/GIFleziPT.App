using GIFleziPT.App.Models;

namespace GIFleziPT.App.Services;

public interface ITaskService
{
    Task<ProcessTaskResult> ProcessTaskAsync(ProcessTaskRequest request);
    Task<GetAzureDevOpsTasksResult> GetAzureDevOpsTasksAsync(AzureDevOpsProfile userProfile);
    Task<AzureDevOpsProfile> GetAzureDevOpsProfileAsync();
    Task RunAsync();
    Task<AzureDevOpsTask> UpdateAzureDevOpsTaskStateAsync(int taskId, string newState, string? comment = null);
}