using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;
using GIFleziPT.App.Configs;
using GIFleziPT.App.Constants;
using GIFleziPT.App.Models;
using Newtonsoft.Json;

namespace GIFleziPT.App.Services;

public class TaskService(ILogger<TaskService> logger, IHttpClientFactory httpClientFactory) : ITaskService
{
    public async Task<ProcessTaskResult> ProcessTaskAsync(ProcessTaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation("Processing task: {TaskName}", request.TaskTitle);

        if (!File.Exists(AppSettings.Instance.PythonScriptPath))
        {
            throw new Exception($"File {AppSettings.Instance.PythonScriptPath} not found");
        }

        // Execute the Python script and capture its output
        string output;
        try
        {
            // Sanitize description for command-line argument
            var safeDescription = ExtractPrompt(request.TaskDescription);
            var arguments = $"--dir \"D:\\Demo\\Demo-10\" --allow-all-tools \"{safeDescription}\"";
            logger.LogInformation("Executing script: python3 \"{ScriptPath}\" {Arguments}", AppSettings.Instance.PythonScriptPath, arguments);
            var startInfo = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{AppSettings.Instance.PythonScriptPath}\" {arguments}",
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
        // Get current user's profile first
        var currentUserProfile = await GetAzureDevOpsProfileAsync();
        var getTasksResult = await GetAzureDevOpsTasksAsync(currentUserProfile);

        logger.LogInformation("RunAsync: Retrieved {TaskCount} new tasks from Azure DevOps - User: {UserDisplayName} ({UserEmailAddress})", getTasksResult.Tasks.Count, currentUserProfile.DisplayName, currentUserProfile.EmailAddress);
        var tasks = getTasksResult.Tasks
            .OrderBy(m => m.ParentId.ToString() ?? m.Title.Split('-', 2).FirstOrDefault())
            .ThenBy(m => m.Title.Split('-', 2).FirstOrDefault())
            .ToList();
        foreach (var task in tasks)
        {
            logger.LogInformation("ProcessTaskAsync: {Id} - {Title} - {State} - ParentId: {ParentId} - ParentTitle: {ParentTitle}", task.Id, task.Title, task.State, task.ParentId, task.ParentTitle);
            try
            {
                var processRequest = new ProcessTaskRequest
                {
                    TaskId = task.Id,
                    TaskTitle = task.Title,
                    TaskDescription = task.Description ?? string.Empty
                };
                var processResult = await ProcessTaskAsync(processRequest);
                logger.LogInformation("\r\nProcessTaskAsync result: TaskName: {TaskName} - Status: {Status} - Output:\r\n{Output}\r\n===========\r\n", processResult.TaskName, processResult.Status, processResult.Output);

                if (processResult.Output.Contains("completed", StringComparison.InvariantCultureIgnoreCase))
                {
                   var comment = $"<p><h5><i>Task automatically completed on {DateTime.Now:yyyy-MM-dd HH:mm:ss}. Output:</i></h5></p><br/><p>{processResult.Output}</p>";
                   await UpdateAzureDevOpsTaskStateAsync(task.Id, "Closed", comment);
                }
                else
                {
                   logger.LogInformation("Task {Id} - {Title} not marked as Closed because output does not indicate completion", task.Id, task.Title);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing task {Id} - {Title} - {State} - ParentId: {ParentId} - ParentTitle: {ParentTitle}", task.Id, task.Title, task.State, task.ParentId, task.ParentTitle);
            }
        }
    }

    #region Azure DevOps Integration
    
    private const string AzureDevOpsApiVersion = "7.0";
    private const string WorkItemsApiPath = "_apis/wit";
    private const string ProfileApiPath = "_apis/profile/profiles/me";
    
    private HttpClient CreateAzureDevOpsClient()
    {
        var client = httpClientFactory.CreateClient();
        var pat = AppSettings.Instance.AzureDevOps.PersonalAccessToken;
        var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{pat}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(AuthSchemes.Basic, authToken);
        return client;
    }
    
    public async Task<AzureDevOpsProfile> GetAzureDevOpsProfileAsync()
    {
        string organization = AppSettings.Instance.AzureDevOps.Organization;

        using var client = CreateAzureDevOpsClient();
        
        var profileUrl = $"https://vssps.dev.azure.com/{organization}/{ProfileApiPath}";
        var profileResponse = await client.GetAsync(profileUrl);
        profileResponse.EnsureSuccessStatusCode();
        var profileJsonString = await profileResponse.Content.ReadAsStringAsync();
        
        var profile = JsonConvert.DeserializeObject<AzureDevOpsProfile>(profileJsonString) ?? new AzureDevOpsProfile();
        
        logger.LogInformation("Retrieved current user profile: {DisplayName} ({EmailAddress})", profile.DisplayName, profile.EmailAddress);
        
        return profile;
    }

    public async Task<GetAzureDevOpsTasksResult> GetAzureDevOpsTasksAsync(AzureDevOpsProfile userProfile)
    {
        string organization = AppSettings.Instance.AzureDevOps.Organization;
        string project = AppSettings.Instance.AzureDevOps.Project;

        var url = $"https://dev.azure.com/{organization}/{project}/{WorkItemsApiPath}/wiql?api-version={AzureDevOpsApiVersion}";
        
        var wiql = new
        {
            query = $"SELECT [System.Id], [System.Title], [System.Description], [System.State], [System.Parent], [System.AssignedTo] FROM WorkItems WHERE [System.WorkItemType] = 'Task' AND ([System.State] = 'New' OR [System.State] = 'Active') AND [System.AssignedTo] = @Me ORDER BY [System.Id] DESC"
        };
        
        logger.LogInformation("User profile - ID: {Id}, Email: {Email}, DisplayName: {DisplayName}", 
            userProfile.Id, userProfile.EmailAddress, userProfile.DisplayName);
        logger.LogInformation("Azure DevOps query: {Query}", wiql.query);

        var result = new GetAzureDevOpsTasksResult();
        using var client = CreateAzureDevOpsClient();

        var wiqlContent = new StringContent(JsonConvert.SerializeObject(wiql), System.Text.Encoding.UTF8, "application/json");
        var wiqlResponse = await client.PostAsync(url, wiqlContent);
        wiqlResponse.EnsureSuccessStatusCode();
        var wiqlResult = System.Text.Json.JsonDocument.Parse(await wiqlResponse.Content.ReadAsStringAsync());
        var workItemRefs = wiqlResult.RootElement.GetProperty("workItems");

        var parentIdSet = new HashSet<int>();
        var tempTasks = new List<AzureDevOpsTask>();
        foreach (var itemRef in workItemRefs.EnumerateArray())
        {
            int id = itemRef.GetProperty("id").GetInt32();
            var workItemUrl = $"https://dev.azure.com/{organization}/{WorkItemsApiPath}/workitems/{id}?api-version={AzureDevOpsApiVersion}";
            var workItemResponse = await client.GetAsync(workItemUrl);
            if (workItemResponse.IsSuccessStatusCode)
            {
                var workItemJson = System.Text.Json.JsonDocument.Parse(await workItemResponse.Content.ReadAsStringAsync());
                var fields = workItemJson.RootElement.GetProperty("fields");
                int? parentId = null;
                string? parentTitle = null;
                string? assignedTo = null;
                string? assignedToId = null;
                string? assignedToEmail = null;
                if (fields.TryGetProperty("System.Parent", out var parentProp))
                {
                    if (parentProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        parentId = parentProp.GetInt32();
                        parentIdSet.Add(parentId.Value);
                    }
                }
                if (fields.TryGetProperty("System.AssignedTo", out var assignedProp))
                {
                    if (assignedProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (assignedProp.TryGetProperty("displayName", out var displayNameProp))
                        {
                            assignedTo = displayNameProp.GetString();
                        }
                        if (assignedProp.TryGetProperty("id", out var idProp))
                        {
                            assignedToId = idProp.GetString();
                        }
                        if (assignedProp.TryGetProperty("uniqueName", out var uniqueNameProp))
                        {
                            assignedToEmail = uniqueNameProp.GetString();
                        }
                    }
                    else if (assignedProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        assignedTo = assignedProp.GetString();
                    }
                }
                string? description = null;
                if (fields.TryGetProperty("System.Description", out var descProp) && descProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    description = descProp.GetString();
                }
                tempTasks.Add(new AzureDevOpsTask
                {
                    Id = id,
                    Title = fields.GetProperty("System.Title").GetString() ?? string.Empty,
                    State = fields.GetProperty("System.State").GetString() ?? string.Empty,
                    Description = description ?? string.Empty,
                    ParentId = parentId,
                    ParentTitle = parentTitle,
                    AssignedTo = assignedTo,
                    AssignedToId = assignedToId,
                    AssignedToEmail = assignedToEmail
                });
            }
        }
        
        // Batch fetch parent titles
        var parentTitles = new Dictionary<int, string>();
        if (parentIdSet.Count > 0)
        {
            var batchUrl = $"https://dev.azure.com/{organization}/{WorkItemsApiPath}/workitemsbatch?api-version={AzureDevOpsApiVersion}";
            var batchRequest = new
            {
                ids = parentIdSet.ToArray(),
                fields = new[] { "System.Title" }
            };
            var batchContent = new StringContent(JsonConvert.SerializeObject(batchRequest), System.Text.Encoding.UTF8, "application/json");
            var batchResponse = await client.PostAsync(batchUrl, batchContent);
            if (batchResponse.IsSuccessStatusCode)
            {
                var batchJson = System.Text.Json.JsonDocument.Parse(await batchResponse.Content.ReadAsStringAsync());
                if (batchJson.RootElement.TryGetProperty("value", out var valueArr))
                {
                    foreach (var parentItem in valueArr.EnumerateArray())
                    {
                        int pid = parentItem.GetProperty("id").GetInt32();
                        var fields = parentItem.GetProperty("fields");
                        var title = fields.GetProperty("System.Title").GetString() ?? string.Empty;
                        parentTitles[pid] = title;
                    }
                }
            }
        }

        logger.LogInformation("Fetched {TaskCount} tasks assigned to current user, {ParentCount} unique parents", tempTasks.Count, parentTitles.Count);
        
        // Assign parent titles and add all tasks (already filtered by WIQL query)
        foreach (var t in tempTasks)
        {
            if (t.ParentId.HasValue && parentTitles.TryGetValue(t.ParentId.Value, out var ptitle))
            {
                t.ParentTitle = ptitle;
            }
            
            // Tasks are already filtered by WIQL, add them all
            logger.LogDebug("Adding task {TaskId}: AssignedTo='{AssignedTo}', AssignedToEmail='{AssignedToEmail}', AssignedToId='{AssignedToId}'", 
                t.Id, t.AssignedTo, t.AssignedToEmail, t.AssignedToId);
            result.Tasks.Add(t);
        }
        return result;
    }

    public async Task<AzureDevOpsTask> UpdateAzureDevOpsTaskStateAsync(int taskId, string newState, string? comment = null)
    {
        logger.LogInformation("Updating task {TaskId} to state {NewState}", taskId, newState);
        string organization = AppSettings.Instance.AzureDevOps.Organization;
        var url = $"https://dev.azure.com/{organization}/{WorkItemsApiPath}/workitems/{taskId}?api-version={AzureDevOpsApiVersion}";
        
        var patchOperations = new List<object>
        {
            new
            {
                op = "replace",
                path = "/fields/System.State",
                value = newState
            }
        };
        
        if (!string.IsNullOrEmpty(comment))
        {
            patchOperations.Add(new
            {
                op = "add",
                path = "/fields/System.History",
                value = comment
            });
        }
        
        var patchDoc = patchOperations.ToArray();
        using var client = CreateAzureDevOpsClient();
        var patchContent = new StringContent(JsonConvert.SerializeObject(patchDoc), System.Text.Encoding.UTF8, "application/json-patch+json");
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

    private static string ExtractPrompt(string? description)
    {
        if (string.IsNullOrEmpty(description)) return string.Empty;
        description = HttpUtility.HtmlDecode(description);
        var idx = description.IndexOf("Prompt:", StringComparison.InvariantCultureIgnoreCase);
        if (idx == -1) return string.Empty;
        description = description[(idx + "Prompt:".Length)..];
        description = Regex.Replace(description, @"<.*?>", string.Empty)
            .Replace("\"", "\\\"")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
        return description;
    }
    #endregion Azure DevOps Integration
}