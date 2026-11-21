using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class SearchProfileRepository : ISearchProfileRepository
{
    private readonly VsrDbContext _context;

    public SearchProfileRepository(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SearchProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SearchProfiles.AsNoTracking().OrderByDescending(p => p.UpdatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<SearchProfile?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.SearchProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<SearchProfile> CreateAsync(SearchProfile profile, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            var count = await _context.SearchProfiles.CountAsync(cancellationToken);
            profile.Id = $"PRF-{1000 + count + 1}";
        }
        _context.SearchProfiles.Add(profile);
        await _context.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<SearchProfile?> UpdateAsync(SearchProfile profile, CancellationToken cancellationToken = default)
    {
        var existing = await _context.SearchProfiles.FirstOrDefaultAsync(p => p.Id == profile.Id, cancellationToken);
        if (existing == null) return null;

        _context.Entry(existing).CurrentValues.SetValues(profile);
        existing.SelectedSources = profile.SelectedSources;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.SearchProfiles.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (existing == null) return false;

        _context.SearchProfiles.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
