using GIFleziPT.App.Configs;
using GIFleziPT.App.Models;
using GIFleziPT.App.Services;
using Microsoft.AspNetCore.Mvc;

namespace GIFleziPT.App.Controllers;

[ApiController]
[Route("tasks")]
public class TasksController(
    ILogger<TasksController> logger,
    ITaskService taskService) : ControllerBase
{
    [HttpPost("process-task")]
    public async Task<IActionResult> ProcessTaskAsync([FromBody] ProcessTaskRequest request)
    {
        logger.LogInformation("Received request to process task.");

        try
        {
            var result = await taskService.ProcessTaskAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProcessTaskAsync error. TaskName: {TaskName}. Message: {Message}", request.TaskName, ex.Message);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("get-tasks")]
    public async Task<IActionResult> GetAzureDevOpsTasks()
    {
        logger.LogInformation("Received request to get Azure DevOps tasks.");

        try
        {
            var result = await taskService.GetAzureDevOpsTasksAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetTasksFromAzureDevOps error. Message: {Message}", ex.Message);
            return StatusCode(500, "Internal server error");
        }
    }
}
