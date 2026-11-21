namespace VsrSalesEngineWebApi.Core.DTOs;

public record AutomationScheduleDto(
    string Id,
    string Name,
    string Description,
    string Frequency,
    string CronExpression,
    string Timezone,
    bool IsEnabled,
    string ProfileId,
    IReadOnlyList<string> SelectedSources,
    int RunCap,
    string QuietHoursStart,
    string QuietHoursEnd,
    DateTime? LastRunAtUtc,
    DateTime NextRunAtUtc,
    string LastRunStatus,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int IntervalHours = 24
);

public record CreateScheduleDto(
    string Name,
    string Description,
    string Frequency,
    string? CronExpression,
    string? Timezone,
    string? ProfileId,
    List<string>? SelectedSources,
    int? RunCap,
    string? QuietHoursStart,
    string? QuietHoursEnd,
    bool? IsEnabled,
    int? IntervalHours = null
);

public record UpdateScheduleDto(
    string Name,
    string Description,
    string Frequency,
    string? CronExpression,
    string? Timezone,
    string? ProfileId,
    List<string>? SelectedSources,
    int? RunCap,
    string? QuietHoursStart,
    string? QuietHoursEnd,
    bool? IsEnabled,
    int? IntervalHours = null
);

public record ToggleScheduleDto(bool IsEnabled);

public record DueRunSummaryDto(
    int SchedulesChecked,
    int SchedulesRun,
    int SchedulesSkipped,
    IReadOnlyList<ScheduleRunExecutionResultDto> Results
);

public record AgentStatusDto(
    string ScheduleId,
    string Name,
    bool IsEnabled,
    int IntervalHours,
    string Timezone,
    string ActiveWindow,
    IReadOnlyList<string> Sources,
    DateTime? LastRunAtUtc,
    DateTime NextRunAtUtc,
    string LastRunStatus
);

public record ScheduleRunExecutionResultDto(
    string ScheduleId,
    bool Success,
    string Status,
    string Message,
    DateTime? NextRunAtUtc,
    string? LockOwner
);
