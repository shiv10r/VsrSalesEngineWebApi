using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Infrastructure.Data;

/// <summary>
/// Earlier versions seeded invented demo rows into every database. This removes exactly those rows (by their
/// fixed ids) so an already-deployed database stops showing made-up leads and counters.
/// Rows the owner created have other ids and are never touched.
/// </summary>
internal static class LegacyDemoDataCleaner
{
    private static readonly string[] Opportunities = { "OP-1001" };
    private static readonly string[] Contacts = { "CNT-1001" };
    private static readonly string[] Sources = { "SRC-001", "SRC-002", "SRC-003", "SRC-004", "SRC-005" };
    private static readonly string[] Schedules = { "SCHED-002" };
    private static readonly string[] Profiles = { "PRF-002" };

    public static async Task RemoveAsync(VsrDbContext context, CancellationToken ct)
    {
        context.Opportunities.RemoveRange(await context.Opportunities.Where(x => Opportunities.Contains(x.Id)).ToListAsync(ct));
        context.Contacts.RemoveRange(await context.Contacts.Where(x => Contacts.Contains(x.Id)).ToListAsync(ct));
        context.Sources.RemoveRange(await context.Sources.Where(x => Sources.Contains(x.Id)).ToListAsync(ct));
        context.Schedules.RemoveRange(await context.Schedules.Where(x => Schedules.Contains(x.Id)).ToListAsync(ct));
        context.SearchProfiles.RemoveRange(await context.SearchProfiles.Where(x => Profiles.Contains(x.Id)).ToListAsync(ct));
        await context.SaveChangesAsync(ct);
    }
}
