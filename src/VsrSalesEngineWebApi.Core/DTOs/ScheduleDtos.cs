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
    DateTime UpdatedAtUtc
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
    bool? IsEnabled
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
    bool? IsEnabled
);

public record ToggleScheduleDto(bool IsEnabled);

public record ScheduleRunExecutionResultDto(
    string ScheduleId,
    bool Success,
    string Status,
    string Message,
    DateTime? NextRunAtUtc,
    string? LockOwner
);
