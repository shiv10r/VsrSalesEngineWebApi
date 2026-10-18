using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services;

public class ScheduleExecutionService : IScheduleExecutionService
{
    private readonly IAutomationScheduleRepository _scheduleRepository;
    private readonly IDistributedLockService _lockService;

    public ScheduleExecutionService(
        IAutomationScheduleRepository scheduleRepository,
        IDistributedLockService lockService)
    {
        _scheduleRepository = scheduleRepository;
        _lockService = lockService;
    }

    public async Task<IReadOnlyList<AutomationScheduleDto>> GetSchedulesAsync()
    {
        var list = await _scheduleRepository.GetAllAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<AutomationScheduleDto?> GetScheduleByIdAsync(string id)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(id);
        return schedule == null ? null : ToDto(schedule);
    }

    public async Task<AutomationScheduleDto> CreateScheduleAsync(CreateScheduleDto dto, string user)
    {
        var entity = new AutomationSchedule
        {
            Id = $"SCHED-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            Frequency = string.IsNullOrWhiteSpace(dto.Frequency) ? "Daily" : dto.Frequency.Trim(),
            CronExpression = dto.CronExpression ?? "0 8 * * 1-5",
            Timezone = dto.Timezone ?? "Asia/Kolkata",
            IsEnabled = dto.IsEnabled.GetValueOrDefault(true),
            ProfileId = dto.ProfileId ?? "DEFAULT_PROFILE",
            SelectedSources = dto.SelectedSources ?? new List<string> { "Company careers", "Public job boards" },
            RunCap = dto.RunCap.GetValueOrDefault(50),
            QuietHoursStart = dto.QuietHoursStart ?? "22:00",
            QuietHoursEnd = dto.QuietHoursEnd ?? "07:00",
            NextRunAtUtc = CalculateNextRun(dto.Frequency ?? "Daily"),
            LastRunStatus = "Ready",
            CreatedBy = user,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var created = await _scheduleRepository.CreateAsync(entity);
        return ToDto(created);
    }

    public async Task<AutomationScheduleDto?> UpdateScheduleAsync(string id, UpdateScheduleDto dto)
    {
        var existing = await _scheduleRepository.GetByIdAsync(id);
        if (existing == null) return null;

        existing.Name = dto.Name.Trim();
        existing.Description = dto.Description.Trim();
        existing.Frequency = dto.Frequency.Trim();
        if (!string.IsNullOrWhiteSpace(dto.CronExpression)) existing.CronExpression = dto.CronExpression.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Timezone)) existing.Timezone = dto.Timezone.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ProfileId)) existing.ProfileId = dto.ProfileId.Trim();
        if (dto.SelectedSources != null) existing.SelectedSources = dto.SelectedSources;
        if (dto.RunCap.HasValue) existing.RunCap = dto.RunCap.Value;
        if (!string.IsNullOrWhiteSpace(dto.QuietHoursStart)) existing.QuietHoursStart = dto.QuietHoursStart.Trim();
        if (!string.IsNullOrWhiteSpace(dto.QuietHoursEnd)) existing.QuietHoursEnd = dto.QuietHoursEnd.Trim();
        if (dto.IsEnabled.HasValue) existing.IsEnabled = dto.IsEnabled.Value;
        existing.NextRunAtUtc = CalculateNextRun(existing.Frequency);
        existing.UpdatedAtUtc = DateTime.UtcNow;

        var updated = await _scheduleRepository.UpdateAsync(existing);
        return updated == null ? null : ToDto(updated);
    }

    public async Task<AutomationScheduleDto?> ToggleScheduleAsync(string id, bool isEnabled)
    {
        var existing = await _scheduleRepository.GetByIdAsync(id);
        if (existing == null) return null;

        existing.IsEnabled = isEnabled;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        if (isEnabled)
        {
            existing.NextRunAtUtc = CalculateNextRun(existing.Frequency);
        }

        var updated = await _scheduleRepository.UpdateAsync(existing);
        return updated == null ? null : ToDto(updated);
    }

    public Task<bool> DeleteScheduleAsync(string id)
    {
        return _scheduleRepository.DeleteAsync(id);
    }

    public async Task<ScheduleRunExecutionResultDto> ExecuteScheduleNowAsync(string scheduleId, string trigger = "Manual")
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
        if (schedule == null)
        {
            return new ScheduleRunExecutionResultDto(
                ScheduleId: scheduleId,
                Success: false,
                Status: "ScheduleNotFound",
                Message: $"Schedule '{scheduleId}' not found.",
                NextRunAtUtc: null,
                LockOwner: null
            );
        }

        var lockKey = $"lock:schedule:{scheduleId}";
        var workerId = $"worker-{Guid.NewGuid():N}";
        var lockAcquired = await _lockService.TryAcquireLockAsync(lockKey, workerId, TimeSpan.FromMinutes(2));

        if (!lockAcquired)
        {
            return new ScheduleRunExecutionResultDto(
                ScheduleId: scheduleId,
                Success: false,
                Status: "ConcurrencyLocked",
                Message: "Another worker or job currently holds the active lease for this schedule. Concurrent execution was safely blocked.",
                NextRunAtUtc: schedule.NextRunAtUtc,
                LockOwner: workerId
            );
        }

        try
        {
            schedule.LastRunAtUtc = DateTime.UtcNow;
            schedule.LastRunStatus = "Success";
            schedule.NextRunAtUtc = CalculateNextRun(schedule.Frequency);
            schedule.UpdatedAtUtc = DateTime.UtcNow;

            await _scheduleRepository.UpdateAsync(schedule);

            return new ScheduleRunExecutionResultDto(
                ScheduleId: scheduleId,
                Success: true,
                Status: "Completed",
                Message: $"Schedule '{schedule.Name}' executed successfully ({trigger}). Next run scheduled for {schedule.NextRunAtUtc:g} UTC.",
                NextRunAtUtc: schedule.NextRunAtUtc,
                LockOwner: workerId
            );
        }
        finally
        {
            await _lockService.ReleaseLockAsync(lockKey, workerId);
        }
    }

    private static DateTime CalculateNextRun(string frequency)
    {
        var now = DateTime.UtcNow;
        return frequency.ToUpperInvariant() switch
        {
            "WEEKLY" => now.AddDays(7),
            _ => now.AddHours(24) // Daily
        };
    }

    private static AutomationScheduleDto ToDto(AutomationSchedule s) => new(
        s.Id,
        s.Name,
        s.Description,
        s.Frequency,
        s.CronExpression,
        s.Timezone,
        s.IsEnabled,
        s.ProfileId,
        s.SelectedSources,
        s.RunCap,
        s.QuietHoursStart,
        s.QuietHoursEnd,
        s.LastRunAtUtc,
        s.NextRunAtUtc,
        s.LastRunStatus,
        s.CreatedBy,
        s.CreatedAtUtc,
        s.UpdatedAtUtc
    );
}
