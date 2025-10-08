using System.Diagnostics;
using GIFleziPT.App.Configs;
using GIFleziPT.App.Constants;
using GIFleziPT.App.Models;

namespace GIFleziPT.App.Services;

public class TaskService(ILogger<TaskService> logger) : ITaskService
{
    public async Task<ProcessTaskResult> ProcessTaskAsync(ProcessTaskRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        logger.LogInformation("Processing task: {TaskName}", request.TaskTitle);

        if (!File.Exists(AppSettings.Instance.PythonScriptPath))
        {
            throw new Exception($"File {AppSettings.Instance.PythonScriptPath} not found");
        }

        // Execute the Python script and capture its output
        string output;
        try
        {
            // Quote and escape the script path and task name to be safe for the shell
            var taskArg = request.TaskTitle?.Replace("\"", "\\\"") ?? string.Empty;
            var startInfo = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{AppSettings.Instance.PythonScriptPath}\" \"{taskArg}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            await process.WaitForExitAsync();
            var stdOut = await process.StandardOutput.ReadToEndAsync();
            var stdErr = await process.StandardError.ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(stdErr) && string.IsNullOrWhiteSpace(stdOut))
            {
                output = $"ERROR: {stdErr.Trim()}";
            }
            else if (!string.IsNullOrWhiteSpace(stdOut) && !string.IsNullOrWhiteSpace(stdErr))
            {
                output = stdOut.Trim() + "\n---STDERR---\n" + stdErr.Trim();
            }
            else
            {
                output = (stdOut + stdErr).Trim();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute python script at {ScriptPath}", AppSettings.Instance.PythonScriptPath);
            output = $"Failed to execute script: {ex.Message}";
        }

        logger.LogInformation("Task processed: {TaskName}", request.TaskTitle);

        return new ProcessTaskResult
        {
            TaskName = request.TaskTitle!,
            Output = output,
            Status = "Processed"
        };
    }

    public async Task RunAsync()
    {
        var getTasksResult = await GetAzureDevOpsTasksAsync();

        logger.LogInformation("RunAsync: Retrieved {TaskCount} tasks from Azure DevOps", getTasksResult.Tasks.Count);
        var tasks = getTasksResult.Tasks.Where(m => m.State == "New" || m.State == "Active").OrderBy(m => m.Title.Split('-', 2).FirstOrDefault()).ToList();
        logger.LogInformation("RunAsync: {NewTaskCount} new tasks to process", tasks.Count);
        foreach (var task in tasks)
        {
            logger.LogInformation("ProcessTaskAsync: {Id} - {Title} - {State}", task.Id, task.Title, task.State);
            try
            {
                var processRequest = new ProcessTaskRequest
                {
                    TaskId = task.Id,
                    TaskTitle = task.Title
                };
                var processResult = await ProcessTaskAsync(processRequest);
                logger.LogInformation("ProcessTaskAsync result: {TaskName} - {Status} - {Output}", processResult.TaskName, processResult.Status, processResult.Output);

                await UpdateAzureDevOpsTaskStateAsync(task.Id, "Closed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing task {Id} - {Title}", task.Id, task.Title);
            }
        }
    }

    #region Azure DevOps Integration
    public async Task<GetAzureDevOpsTasksResult> GetAzureDevOpsTasksAsync()
    {
        string organization = AppSettings.Instance.AzureDevOps.Organization;
        string project = AppSettings.Instance.AzureDevOps.Project;
        string pat = AppSettings.Instance.AzureDevOps.PersonalAccessToken;

        var url = $"https://dev.azure.com/{organization}/{project}/_apis/wit/wiql?api-version=7.0";
        var wiql = new
        {
            query = "SELECT [System.Id], [System.Title], [System.State] FROM WorkItems WHERE [System.WorkItemType] = 'Task' ORDER BY [System.Id] DESC"
        };

        var result = new GetAzureDevOpsTasksResult();
        using var client = new HttpClient();
        var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{pat}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(AuthSchemes.Basic, authToken);

        var wiqlContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");
        var wiqlResponse = await client.PostAsync(url, wiqlContent);
        wiqlResponse.EnsureSuccessStatusCode();
        var wiqlResult = System.Text.Json.JsonDocument.Parse(await wiqlResponse.Content.ReadAsStringAsync());
        var workItemRefs = wiqlResult.RootElement.GetProperty("workItems");

        foreach (var itemRef in workItemRefs.EnumerateArray())
        {
            int id = itemRef.GetProperty("id").GetInt32();
            var workItemUrl = $"https://dev.azure.com/{organization}/_apis/wit/workitems/{id}?api-version=7.0";
            var workItemResponse = await client.GetAsync(workItemUrl);
            if (workItemResponse.IsSuccessStatusCode)
            {
                var workItemJson = System.Text.Json.JsonDocument.Parse(await workItemResponse.Content.ReadAsStringAsync());
                var fields = workItemJson.RootElement.GetProperty("fields");
                result.Tasks.Add(new AzureDevOpsTask
                {
                    Id = id,
                    Title = fields.GetProperty("System.Title").GetString() ?? string.Empty,
                    State = fields.GetProperty("System.State").GetString() ?? string.Empty
                });
            }
        }
        return result;
    }

    public async Task<AzureDevOpsTask> UpdateAzureDevOpsTaskStateAsync(int taskId, string newState)
    {
        logger.LogInformation("Updating task {TaskId} to state {NewState}", taskId, newState);
        string organization = AppSettings.Instance.AzureDevOps.Organization;
        string project = AppSettings.Instance.AzureDevOps.Project;
        string pat = AppSettings.Instance.AzureDevOps.PersonalAccessToken;
        var url = $"https://dev.azure.com/{organization}/_apis/wit/workitems/{taskId}?api-version=7.0";
        var patchDoc = new[]
        {
            new
            {
                op = "replace",
                path = "/fields/System.State",
                value = newState
            }
        };
        using var client = new HttpClient();
        var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{pat}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(AuthSchemes.Basic, authToken);
        var patchContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(patchDoc), System.Text.Encoding.UTF8, "application/json-patch+json");
        var response = await client.PatchAsync(url, patchContent);
        response.EnsureSuccessStatusCode();
        var workItemJson = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var fields = workItemJson.RootElement.GetProperty("fields");

        logger.LogInformation("Task {TaskId} updated to state {NewState}", taskId, newState);
        return new AzureDevOpsTask
        {
            Id = taskId,
            Title = fields.GetProperty("System.Title").GetString() ?? string.Empty,
            State = fields.GetProperty("System.State").GetString() ?? string.Empty
        };
    }
    #endregion Azure DevOps Integration
}