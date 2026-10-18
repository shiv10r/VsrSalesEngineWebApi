using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Interfaces;

public interface IAutomationScheduleRepository
{
    Task<IReadOnlyList<AutomationSchedule>> GetAllAsync();
    Task<AutomationSchedule?> GetByIdAsync(string id);
    Task<AutomationSchedule> CreateAsync(AutomationSchedule schedule);
    Task<AutomationSchedule?> UpdateAsync(AutomationSchedule schedule);
    Task<bool> DeleteAsync(string id);
}
