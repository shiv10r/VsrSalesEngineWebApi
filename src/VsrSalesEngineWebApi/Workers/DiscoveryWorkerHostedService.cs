using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Workers;

public class DiscoveryWorkerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DiscoveryWorkerHostedService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    public DiscoveryWorkerHostedService(IServiceScopeFactory scopeFactory, ILogger<DiscoveryWorkerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Autonomous discovery worker started. Polling every {Interval}s for due schedules.", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueSchedulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Autonomous discovery worker tick failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunDueSchedulesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduleRepository = scope.ServiceProvider.GetRequiredService<IAutomationScheduleRepository>();
        var scheduleExecutionService = scope.ServiceProvider.GetRequiredService<IScheduleExecutionService>();

        var schedules = await scheduleRepository.GetAllAsync();
        var now = DateTime.UtcNow;

        foreach (var schedule in schedules)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (!schedule.IsEnabled) continue;
            if (schedule.NextRunAtUtc > now) continue;

            _logger.LogInformation("Autonomous worker triggering due schedule '{ScheduleId}' ({Name}).", schedule.Id, schedule.Name);

            var result = await scheduleExecutionService.ExecuteScheduleNowAsync(schedule.Id, "AutonomousWorker");

            if (!result.Success)
            {
                _logger.LogWarning("Autonomous schedule run for '{ScheduleId}' did not complete: {Status} - {Message}", schedule.Id, result.Status, result.Message);
            }
            else
            {
                _logger.LogInformation("Autonomous schedule run for '{ScheduleId}' completed: {Message}", schedule.Id, result.Message);
            }
        }
    }
}
