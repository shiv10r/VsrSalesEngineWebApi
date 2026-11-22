using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services.LocalLeads;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class LocalLeadRepository : ILocalLeadRepository
{
    private readonly VsrDbContext _context;

    public LocalLeadRepository(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<LocalLead> Items, int Total)> QueryAsync(LocalLeadFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _context.LocalLeads.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.District))
        {
            var district = (UttarPradesh.Resolve(filter.District) ?? filter.District).Trim().ToLower();
            query = query.Where(l => l.District.ToLower() == district);
        }
        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var category = filter.Category.Trim().ToLower();
            query = query.Where(l => l.Category.ToLower() == category);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim().ToLower();
            query = query.Where(l => l.Status.ToLower() == status);
        }
        if (filter.HasWebsite is { } hasWebsite) query = query.Where(l => l.HasWebsite == hasWebsite);
        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim().ToLower();
            query = query.Where(l => l.BusinessName.ToLower().Contains(q) || l.Address.ToLower().Contains(q) ||
                                     l.Phone.Contains(q) || l.Notes.ToLower().Contains(q));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc).ThenBy(l => l.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<LocalLead?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _context.LocalLeads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<LocalLead?> GetByPlaceIdAsync(string placeId, CancellationToken cancellationToken = default) =>
        _context.LocalLeads.AsNoTracking().FirstOrDefaultAsync(l => l.PlaceId == placeId, cancellationToken);

    public async Task<LocalLead> AddAsync(LocalLead lead, CancellationToken cancellationToken = default)
    {
        _context.LocalLeads.Add(lead);
        await _context.SaveChangesAsync(cancellationToken);
        return lead;
    }

    public async Task AddRangeAsync(IReadOnlyCollection<LocalLead> leads, CancellationToken cancellationToken = default)
    {
        _context.LocalLeads.AddRange(leads);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<LocalLead> UpdateAsync(LocalLead lead, CancellationToken cancellationToken = default)
    {
        // Copy onto the tracked row so this also works when the same context already tracks the lead.
        var tracked = await _context.LocalLeads.FirstOrDefaultAsync(l => l.Id == lead.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Local lead '{lead.Id}' no longer exists.");

        _context.Entry(tracked).CurrentValues.SetValues(lead);
        await _context.SaveChangesAsync(cancellationToken);
        return tracked;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.LocalLeads.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (existing is null) return false;

        _context.LocalLeads.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> DuplicateExistsAsync(string normalizedName, string district, string normalizedPhone, string? excludeId, CancellationToken cancellationToken = default) =>
        _context.LocalLeads.AsNoTracking().AnyAsync(l =>
            l.Id != excludeId &&
            ((l.NormalizedName == normalizedName && l.District == district) ||
             (normalizedPhone != "" && l.NormalizedPhone == normalizedPhone)),
            cancellationToken);

    public async Task<(HashSet<string> NameKeys, HashSet<string> Phones)> GetDuplicateKeysAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.LocalLeads.AsNoTracking()
            .Select(l => new { l.NormalizedName, l.District, l.NormalizedPhone })
            .ToListAsync(cancellationToken);

        var nameKeys = rows.Select(r => LocalLeadBuilder.DuplicateKey(r.NormalizedName, r.District)).ToHashSet();
        var phones = rows.Where(r => r.NormalizedPhone.Length > 0).Select(r => r.NormalizedPhone).ToHashSet();
        return (nameKeys, phones);
    }

    public async Task<IReadOnlyList<LocalLeadSummaryRow>> GetSummaryRowsAsync(CancellationToken cancellationToken = default) =>
        await _context.LocalLeads.AsNoTracking()
            .Select(l => new LocalLeadSummaryRow(l.District, l.Category, l.HasWebsite))
            .ToListAsync(cancellationToken);
}
