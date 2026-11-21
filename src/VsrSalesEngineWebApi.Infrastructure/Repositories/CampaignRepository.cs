using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class CampaignRepository : ICampaignRepository
{
    private readonly VsrDbContext _context;

    public CampaignRepository(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Campaign>> GetAllAsync(string? status = null)
    {
        var query = _context.Campaigns.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var all = await query.ToListAsync();
            return all.Where(c => c.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.UpdatedAtUtc).ToList();
        }

        return await query.OrderByDescending(c => c.UpdatedAtUtc).ToListAsync();
    }

    public async Task<Campaign?> GetByIdAsync(string id)
    {
        return await _context.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Campaign> CreateAsync(Campaign campaign)
    {
        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();
        return campaign;
    }

    public async Task<Campaign?> UpdateAsync(Campaign campaign)
    {
        var existing = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == campaign.Id);
        if (existing == null) return null;

        _context.Entry(existing).CurrentValues.SetValues(campaign);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var existing = await _context.Campaigns.FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null) return false;

        _context.Campaigns.Remove(existing);
        await _context.SaveChangesAsync();
        return true;
    }
}
