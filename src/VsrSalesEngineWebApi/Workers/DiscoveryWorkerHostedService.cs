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
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<IDueScheduleRunner>();
                var summary = await runner.RunDueAsync("AutonomousWorker", stoppingToken);

                if (summary.SchedulesRun + summary.SchedulesSkipped > 0)
                {
                    _logger.LogInformation("Worker tick: {Run} run, {Skipped} skipped of {Checked} schedules.",
                        summary.SchedulesRun, summary.SchedulesSkipped, summary.SchedulesChecked);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Autonomous discovery worker tick failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
