using GIFleziPT.App.Models;
using GIFleziPT.App.Services;
using Microsoft.AspNetCore.Mvc;

namespace GIFleziPT.App.Controllers;

[ApiController]
[Route("[controller]")]
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
            logger.LogError(ex, "Error processing task: {TaskName}", request.TaskName);
            return StatusCode(500, "Internal server error");
        }
    }
}
