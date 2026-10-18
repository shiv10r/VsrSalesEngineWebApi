using VsrSalesEngineWebApi.Core.DTOs;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IScheduleExecutionService
{
    Task<IReadOnlyList<AutomationScheduleDto>> GetSchedulesAsync();
    Task<AutomationScheduleDto?> GetScheduleByIdAsync(string id);
    Task<AutomationScheduleDto> CreateScheduleAsync(CreateScheduleDto dto, string user);
    Task<AutomationScheduleDto?> UpdateScheduleAsync(string id, UpdateScheduleDto dto);
    Task<AutomationScheduleDto?> ToggleScheduleAsync(string id, bool isEnabled);
    Task<bool> DeleteScheduleAsync(string id);

    Task<ScheduleRunExecutionResultDto> ExecuteScheduleNowAsync(string scheduleId, string trigger = "Manual");
}
