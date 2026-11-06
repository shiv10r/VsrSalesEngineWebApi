using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class AutomationScheduleRepository : IAutomationScheduleRepository
{
    private readonly VsrDbContext _context;

    public AutomationScheduleRepository(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AutomationSchedule>> GetAllAsync()
    {
        return await _context.Schedules.AsNoTracking().OrderByDescending(s => s.UpdatedAtUtc).ToListAsync();
    }

    public async Task<AutomationSchedule?> GetByIdAsync(string id)
    {
        return await _context.Schedules.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<AutomationSchedule> CreateAsync(AutomationSchedule schedule)
    {
        _context.Schedules.Add(schedule);
        await _context.SaveChangesAsync();
        return schedule;
    }

    public async Task<AutomationSchedule?> UpdateAsync(AutomationSchedule schedule)
    {
        var existing = await _context.Schedules.FirstOrDefaultAsync(s => s.Id == schedule.Id);
        if (existing == null) return null;

        _context.Entry(existing).CurrentValues.SetValues(schedule);
        existing.SelectedSources = schedule.SelectedSources;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var existing = await _context.Schedules.FirstOrDefaultAsync(s => s.Id == id);
        if (existing == null) return false;

        _context.Schedules.Remove(existing);
        await _context.SaveChangesAsync();
        return true;
    }
}
