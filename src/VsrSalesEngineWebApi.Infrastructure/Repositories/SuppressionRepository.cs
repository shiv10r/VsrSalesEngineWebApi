using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class SuppressionRepository : ISuppressionRepository
{
    private readonly VsrDbContext _context;

    public SuppressionRepository(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SuppressionRecord>> GetAllAsync(string? query = null)
    {
        var all = await _context.Suppressions.AsNoTracking().ToListAsync();
        var q = all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            q = q.Where(s => s.Target.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             s.Reason.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return q.OrderByDescending(s => s.CreatedAtUtc).ToList();
    }

    public async Task<SuppressionRecord?> GetByIdAsync(string id)
    {
        return await _context.Suppressions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<bool> IsSuppressedAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var all = await _context.Suppressions.AsNoTracking().ToListAsync();

        var directMatch = all.Any(s =>
            s.Type.Equals("Email", StringComparison.OrdinalIgnoreCase) &&
            s.Target.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));

        if (directMatch) return true;

        var atIndex = normalizedEmail.IndexOf('@');
        if (atIndex >= 0 && atIndex < normalizedEmail.Length - 1)
        {
            var domain = normalizedEmail[(atIndex + 1)..];
            var domainMatch = all.Any(s =>
                s.Type.Equals("Domain", StringComparison.OrdinalIgnoreCase) &&
                (s.Target.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                 s.Target.Equals("@" + domain, StringComparison.OrdinalIgnoreCase)));

            if (domainMatch) return true;
        }

        return false;
    }

    public async Task<SuppressionRecord> AddAsync(SuppressionRecord record)
    {
        _context.Suppressions.Add(record);
        await _context.SaveChangesAsync();
        return record;
    }

    public async Task<bool> RemoveAsync(string id)
    {
        var existing = await _context.Suppressions.FirstOrDefaultAsync(s => s.Id == id);
        if (existing == null) return false;

        _context.Suppressions.Remove(existing);
        await _context.SaveChangesAsync();
        return true;
    }
}
