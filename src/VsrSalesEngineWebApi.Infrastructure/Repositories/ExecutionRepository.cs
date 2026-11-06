using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class ExecutionRepository : IExecutionRepository
{
    private readonly VsrDbContext _context;

    public ExecutionRepository(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<ExecutionRecord?> GetByIdAsync(string id)
    {
        return await _context.Executions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IReadOnlyList<ExecutionRecord>> GetAllAsync(string? jobType = null, int limit = 50)
    {
        var all = await _context.Executions.AsNoTracking().ToListAsync();
        var query = all.AsEnumerable();
        if (!string.IsNullOrEmpty(jobType))
        {
            query = query.Where(r => r.JobType.Equals(jobType, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(r => r.CreatedAtUtc).Take(limit).ToList();
    }

    public async Task<IReadOnlyList<ExecutionRecord>> GetFailuresAsync(int limit = 50)
    {
        var all = await _context.Executions.AsNoTracking().ToListAsync();
        return all
            .Where(r => r.Status.StartsWith("Failed") || r.Status == "DeadLetter")
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<ExecutionRecord>> GetDeadLettersAsync(int limit = 50)
    {
        var all = await _context.Executions.AsNoTracking().ToListAsync();
        return all
            .Where(r => r.Status == "DeadLetter")
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(limit)
            .ToList();
    }

    public async Task AddAsync(ExecutionRecord record)
    {
        _context.Executions.Add(record);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ExecutionRecord record)
    {
        var existing = await _context.Executions.FirstOrDefaultAsync(r => r.Id == record.Id);
        if (existing == null) return;

        _context.Entry(existing).CurrentValues.SetValues(record);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, int>> GetSourceFailureCountsAsync()
    {
        var all = await _context.SourceFailureCounts.AsNoTracking().ToListAsync();
        return all.ToDictionary(f => f.SourceName, f => f.Count);
    }

    public async Task RecordSourceFailureAsync(string sourceName)
    {
        var existing = await _context.SourceFailureCounts.FirstOrDefaultAsync(f => f.SourceName == sourceName);
        if (existing == null)
        {
            _context.SourceFailureCounts.Add(new Data.SourceFailureCount { SourceName = sourceName, Count = 1 });
        }
        else
        {
            existing.Count += 1;
        }

        await _context.SaveChangesAsync();
    }
}
