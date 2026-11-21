using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class SourceRegistryRepository : ISourceRegistryRepository
{
    private readonly VsrDbContext _context;

    public SourceRegistryRepository(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SourceDefinition>> GetAllSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Sources.AsNoTracking().OrderBy(s => s.Id).ToListAsync(cancellationToken);
    }

    public async Task<SourceDefinition?> GetSourceByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _context.Sources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<SourceDefinition?> UpdatePolicyStatusAsync(string id, string status, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Sources.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (existing == null) return null;

        existing.PolicyStatus = status;
        existing.LastCheckedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task IncrementDailyRequestCountAsync(string id, int count = 1, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Sources.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (existing == null) return;

        existing.CurrentDayRequests += count;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
