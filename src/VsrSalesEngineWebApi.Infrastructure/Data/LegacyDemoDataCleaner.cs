using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Infrastructure.Data;

/// <summary>
/// Earlier versions seeded invented demo rows into every database. This removes exactly those rows (by their
/// fixed ids) so an already-deployed database stops showing made-up leads, campaigns and counters.
/// Rows the owner created have other ids and are never touched.
/// </summary>
internal static class LegacyDemoDataCleaner
{
    private static readonly string[] Opportunities = { "OP-1001" };
    private static readonly string[] Contacts = { "CNT-1001" };
    private static readonly string[] Campaigns = { "CMP-001", "CMP-002", "CMP-003" };
    private static readonly string[] Executions = { "EXEC-001", "EXEC-002", "EXEC-003" };
    private static readonly string[] Suppressions = { "SUP-001", "SUP-002", "SUP-003" };
    private static readonly string[] Sources = { "SRC-001", "SRC-002", "SRC-003", "SRC-004", "SRC-005" };
    private static readonly string[] Schedules = { "SCHED-002" };
    private static readonly string[] Profiles = { "PRF-002" };

    public static async Task RemoveAsync(VsrDbContext context, CancellationToken ct)
    {
        context.Opportunities.RemoveRange(await context.Opportunities.Where(x => Opportunities.Contains(x.Id)).ToListAsync(ct));
        context.Contacts.RemoveRange(await context.Contacts.Where(x => Contacts.Contains(x.Id)).ToListAsync(ct));
        context.Campaigns.RemoveRange(await context.Campaigns.Where(x => Campaigns.Contains(x.Id)).ToListAsync(ct));
        context.Executions.RemoveRange(await context.Executions.Where(x => Executions.Contains(x.Id)).ToListAsync(ct));
        context.Suppressions.RemoveRange(await context.Suppressions.Where(x => Suppressions.Contains(x.Id)).ToListAsync(ct));
        context.Sources.RemoveRange(await context.Sources.Where(x => Sources.Contains(x.Id)).ToListAsync(ct));
        context.Schedules.RemoveRange(await context.Schedules.Where(x => Schedules.Contains(x.Id)).ToListAsync(ct));
        context.SearchProfiles.RemoveRange(await context.SearchProfiles.Where(x => Profiles.Contains(x.Id)).ToListAsync(ct));
        context.SourceFailureCounts.RemoveRange(await context.SourceFailureCounts.Where(x => x.SourceName == "Company careers").ToListAsync(ct));
        await context.SaveChangesAsync(ct);
    }
}
