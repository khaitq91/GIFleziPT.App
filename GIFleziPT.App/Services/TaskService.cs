using System.Diagnostics;
using GIFleziPT.App.Configs;
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

        logger.LogInformation("Processing task: {TaskName}", request.TaskName);

        if (!File.Exists(AppSettings.Instance.PythonScriptPath))
        {
            throw new Exception($"File {AppSettings.Instance.PythonScriptPath} not found");
        }

        // Execute the Python script and capture its output
        string output;
        try
        {
            // Quote and escape the script path and task name to be safe for the shell
            var taskArg = request.TaskName?.Replace("\"", "\\\"") ?? string.Empty;
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

        logger.LogInformation("Task processed: {TaskName}", request.TaskName);

        return new ProcessTaskResult
        {
            TaskName = request.TaskName!,
            Output = output,
            Status = "Processed"
        };
    }
}
