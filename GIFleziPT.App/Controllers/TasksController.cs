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
        logger.LogInformation("Received request ProcessTaskAsync");

        try
        {
            var result = await taskService.ProcessTaskAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProcessTaskAsync error. TaskId: {TaskId}, TaskTitle: {TaskTitle}. Message: {Message}", request.TaskTitle, request.TaskTitle, ex.Message);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("get-tasks")]
    public async Task<IActionResult> GetAzureDevOpsTasksAsync()
    {
        logger.LogInformation("Received request GetAzureDevOpsTasksAsync");

        try
        {
            var result = await taskService.GetAzureDevOpsTasksAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetAzureDevOpsTasksAsync error. Message: {Message}", ex.Message);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("run")]
    public async Task<IActionResult> RunAsync()
    {
        logger.LogInformation("Received request RunAsync");

        try
        {
            await taskService.RunAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RunAsync error. Message: {Message}", ex.Message);
            return StatusCode(500, "Internal server error");
        }
    }
}
