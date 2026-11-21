namespace VsrSalesEngineWebApi.Core.DTOs;

public record AgentSettingDto(string Id, string Name, string DefaultName, string Role, bool Enabled, bool Required);

public record AgentKeywordsDto(IReadOnlyList<string> Target, IReadOnlyList<string> Excluded);

public record ShiftSettingDto(
    string ScheduleId,
    string Name,
    bool Enabled,
    int IntervalHours,
    IReadOnlyList<string> Sources,
    string ActiveFrom,
    string ActiveTo,
    string Timezone);

public record AgentSettingsDto(
    IReadOnlyList<AgentSettingDto> Agents,
    int DraftThreshold,
    AgentKeywordsDto Keywords,
    IReadOnlyList<ShiftSettingDto> Shifts,
    IReadOnlyList<string> AvailableSources,
    string? UpdatedBy,
    DateTime? UpdatedAtUtc);

public record UpdateAgentDto(string Id, string Name, bool Enabled);

public record UpdateKeywordsDto(List<string>? Target, List<string>? Excluded);

public record UpdateShiftDto(
    string ScheduleId,
    bool Enabled,
    int IntervalHours,
    List<string>? Sources,
    string? ActiveFrom,
    string? ActiveTo);

public record UpdateAgentSettingsDto(
    List<UpdateAgentDto>? Agents,
    int DraftThreshold,
    UpdateKeywordsDto? Keywords,
    List<UpdateShiftDto>? Shifts);

public record ValidationErrorDto(string Field, string Message);

public record SettingsErrorsDto(IReadOnlyList<ValidationErrorDto> Errors);
