using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class OutreachTemplateRepository : IOutreachTemplateRepository
{
    private readonly VsrDbContext _context;

    public OutreachTemplateRepository(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OutreachTemplate>> GetAllAsync(string? category = null)
    {
        var all = await _context.OutreachTemplates.AsNoTracking().ToListAsync();
        var query = all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(t => t.UpdatedAtUtc).ToList();
    }

    public async Task<OutreachTemplate?> GetByIdAsync(string id)
    {
        return await _context.OutreachTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<OutreachTemplate> CreateAsync(OutreachTemplate template)
    {
        _context.OutreachTemplates.Add(template);
        await _context.SaveChangesAsync();
        return template;
    }

    public async Task<OutreachTemplate?> UpdateAsync(OutreachTemplate template)
    {
        var existing = await _context.OutreachTemplates.FirstOrDefaultAsync(t => t.Id == template.Id);
        if (existing == null) return null;

        _context.Entry(existing).CurrentValues.SetValues(template);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var existing = await _context.OutreachTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (existing == null) return false;

        _context.OutreachTemplates.Remove(existing);
        await _context.SaveChangesAsync();
        return true;
    }
}
