using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class DueScheduleRunner : IDueScheduleRunner
{
    private readonly IAutomationScheduleRepository _scheduleRepository;
    private readonly IScheduleExecutionService _executionService;

    public DueScheduleRunner(
        IAutomationScheduleRepository scheduleRepository,
        IScheduleExecutionService executionService)
    {
        _scheduleRepository = scheduleRepository;
        _executionService = executionService;
    }

    public async Task<DueRunSummaryDto> RunDueAsync(string trigger, CancellationToken cancellationToken = default)
    {
        var schedules = await _scheduleRepository.GetAllAsync();
        var now = DateTime.UtcNow;
        var results = new List<ScheduleRunExecutionResultDto>();

        foreach (var schedule in schedules)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (!schedule.IsEnabled || schedule.NextRunAtUtc > now) continue;

            results.Add(await _executionService.ExecuteScheduleNowAsync(schedule.Id, trigger));
        }

        var skipped = results.Count(r => r.Status is "SkippedQuietHours" or "ConcurrencyLocked" or "RelayBusy");
        return new DueRunSummaryDto(schedules.Count, results.Count - skipped, skipped, results);
    }

    public async Task<IReadOnlyList<AgentStatusDto>> GetStatusAsync()
    {
        var schedules = await _scheduleRepository.GetAllAsync();
        return schedules.Select(ToStatus).ToList();
    }

    private static AgentStatusDto ToStatus(AutomationSchedule s) => new(
        s.Id,
        s.Name,
        s.IsEnabled,
        s.IntervalHours,
        s.Timezone,
        ActiveWindow: $"{s.QuietHoursEnd}-{s.QuietHoursStart}",
        s.SelectedSources,
        s.LastRunAtUtc,
        s.NextRunAtUtc,
        s.LastRunStatus);
}
