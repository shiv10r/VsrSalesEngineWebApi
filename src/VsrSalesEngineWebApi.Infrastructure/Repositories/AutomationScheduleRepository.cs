using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class AutomationScheduleRepository : IAutomationScheduleRepository
{
    private static readonly ConcurrentDictionary<string, AutomationSchedule> Schedules = new(StringComparer.OrdinalIgnoreCase);

    static AutomationScheduleRepository()
    {
        var seed = new List<AutomationSchedule>
        {
            new()
            {
                Id = "SCHED-001",
                Name = "Daily opportunity discovery",
                Description = "Company career pages and permitted public feeds",
                Frequency = "Daily",
                CronExpression = "0 8 * * 1-5",
                Timezone = "Asia/Kolkata",
                IsEnabled = true,
                ProfileId = "PROF-001",
                SelectedSources = new List<string> { "Company careers", "Approved feeds", "Public job boards" },
                RunCap = 50,
                QuietHoursStart = "22:00",
                QuietHoursEnd = "07:00",
                LastRunAtUtc = DateTime.UtcNow.AddHours(-16),
                NextRunAtUtc = DateTime.UtcNow.AddHours(8),
                LastRunStatus = "Success",
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-14),
                UpdatedAtUtc = DateTime.UtcNow.AddHours(-16)
            },
            new()
            {
                Id = "SCHED-002",
                Name = "Weekly procurement scan",
                Description = "Selected public procurement portals and tenders",
                Frequency = "Weekly",
                CronExpression = "0 9 * * 1",
                Timezone = "Asia/Kolkata",
                IsEnabled = false,
                ProfileId = "PROF-002",
                SelectedSources = new List<string> { "Public procurement" },
                RunCap = 30,
                QuietHoursStart = "22:00",
                QuietHoursEnd = "07:00",
                LastRunAtUtc = DateTime.UtcNow.AddDays(-6),
                NextRunAtUtc = DateTime.UtcNow.AddDays(1),
                LastRunStatus = "Success",
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-14),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-6)
            }
        };

        foreach (var s in seed)
        {
            Schedules[s.Id] = s;
        }
    }

    public Task<IReadOnlyList<AutomationSchedule>> GetAllAsync()
    {
        var list = Schedules.Values.OrderByDescending(s => s.UpdatedAtUtc).ToList();
        return Task.FromResult<IReadOnlyList<AutomationSchedule>>(list);
    }

    public Task<AutomationSchedule?> GetByIdAsync(string id)
    {
        Schedules.TryGetValue(id, out var schedule);
        return Task.FromResult(schedule);
    }

    public Task<AutomationSchedule> CreateAsync(AutomationSchedule schedule)
    {
        Schedules[schedule.Id] = schedule;
        return Task.FromResult(schedule);
    }

    public Task<AutomationSchedule?> UpdateAsync(AutomationSchedule schedule)
    {
        if (!Schedules.ContainsKey(schedule.Id)) return Task.FromResult<AutomationSchedule?>(null);
        Schedules[schedule.Id] = schedule;
        return Task.FromResult<AutomationSchedule?>(schedule);
    }

    public Task<bool> DeleteAsync(string id)
    {
        var removed = Schedules.TryRemove(id, out _);
        return Task.FromResult(removed);
    }
}
