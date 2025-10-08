using GIFleziPT.App.Configs;

namespace GIFleziPT.App.Services;

public class TaskRunnerJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaskRunnerJob> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(10);
    private volatile bool _isRunning = false;

    public TaskRunnerJob(IServiceProvider serviceProvider, ILogger<TaskRunnerJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        _interval = AppSettings.Instance.TaskRunnerJobIntervalSeconds > 0
            ? TimeSpan.FromSeconds(AppSettings.Instance.TaskRunnerJobIntervalSeconds)
            : TimeSpan.FromMinutes(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TaskRunnerJob started. Waiting {delay}s for startup delay...", (int)_startupDelay.TotalSeconds);
        await Task.Delay(_startupDelay, stoppingToken);
        _logger.LogInformation("Startup delay complete. Starting scheduled job loop for each {internal}s", (int)_interval.TotalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_isRunning)
            {
                _isRunning = true;
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var taskService = scope.ServiceProvider.GetRequiredService<ITaskService>();
                    await taskService.RunAsync();
                    _logger.LogInformation("TaskRunnerJob: RunAsync completed.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TaskRunnerJob: Error running scheduled task.");
                }
                finally
                {
                    _isRunning = false;
                }
            }
            else
            {
                _logger.LogWarning("TaskRunnerJob: Previous job still running, skipping this interval.");
            }
            _logger.LogInformation("TaskRunnerJob: Waiting {interval}s until next run.", (int)_interval.TotalSeconds);
            await Task.Delay(_interval, stoppingToken);

        }
        _logger.LogInformation("TaskRunnerJob stopped.");
    }
}
