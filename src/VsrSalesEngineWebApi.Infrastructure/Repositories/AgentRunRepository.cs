using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class AgentRunRepository : IAgentRunRepository
{
    private readonly VsrDbContext _context;

    public AgentRunRepository(VsrDbContext context)
    {
        _context = context;
    }

    public async Task<AgentRun> AddRunAsync(AgentRun run)
    {
        _context.AgentRuns.Add(run);
        await _context.SaveChangesAsync();
        return run;
    }

    public async Task UpdateRunAsync(AgentRun run)
    {
        _context.AgentRuns.Update(run);
        await _context.SaveChangesAsync();
    }

    // Tracked on purpose: BeginRun and Execute share one scope, and Update must see the same instance.
    public Task<AgentRun?> GetRunAsync(string id) =>
        _context.AgentRuns.FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IReadOnlyList<AgentRun>> GetRunsAsync(int take) =>
        await _context.AgentRuns.AsNoTracking()
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync();

    public Task<AgentRun?> GetRunningAsync(DateTime notOlderThanUtc) =>
        _context.AgentRuns.AsNoTracking()
            .Where(r => r.Status == "running" && r.StartedAtUtc >= notOlderThanUtc)
            .OrderByDescending(r => r.StartedAtUtc)
            .FirstOrDefaultAsync();

    public async Task<int> FailStaleRunsAsync(DateTime olderThanUtc)
    {
        var stale = await _context.AgentRuns
            .Where(r => r.Status == "running" && r.StartedAtUtc < olderThanUtc)
            .ToListAsync();

        foreach (var run in stale)
        {
            run.Status = "failed";
            run.FinishedAtUtc = DateTime.UtcNow;
            run.Summary = "Run did not finish (the server stopped or restarted mid-run).";
        }

        if (stale.Count > 0) await _context.SaveChangesAsync();
        return stale.Count;
    }

    public async Task<AgentEvent> AddEventAsync(AgentEvent agentEvent)
    {
        _context.AgentEvents.Add(agentEvent);
        await _context.SaveChangesAsync();
        return agentEvent;
    }

    public async Task<IReadOnlyList<AgentEvent>> GetEventsAsync(long afterId, int take, string? runId)
    {
        var query = _context.AgentEvents.AsNoTracking().Where(e => e.Id > afterId);
        if (!string.IsNullOrWhiteSpace(runId)) query = query.Where(e => e.RunId == runId);

        return await query.OrderBy(e => e.Id).Take(Math.Clamp(take, 1, 500)).ToListAsync();
    }

    public async Task<IReadOnlyList<DateTime>> GetRunStartTimesAsync(DateTime sinceUtc) =>
        await _context.AgentRuns.AsNoTracking()
            .Where(r => r.StartedAtUtc >= sinceUtc)
            .Select(r => r.StartedAtUtc)
            .ToListAsync();

    public Task<AgentEvent?> GetLatestEventAsync(string runId) =>
        _context.AgentEvents.AsNoTracking()
            .Where(e => e.RunId == runId)
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<AgentEventTotal>> GetTotalsAsync() =>
        await _context.AgentEvents.AsNoTracking()
            .GroupBy(e => e.AgentId)
            .Select(g => new AgentEventTotal(
                g.Key,
                g.Count(),
                g.Count(x => x.LeadJson != null),
                g.Max(x => (DateTime?)x.AtUtc)))
            .ToListAsync();
}
