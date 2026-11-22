using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Infrastructure.Data;

namespace VsrSalesEngineWebApi.Tests;

/// <summary>
/// Production seeds the live feeds and shift schedules switched OFF. Most tests exercise running agents, so
/// this test-only helper switches them on, and adds a source that has no adapter and a second profile.
/// </summary>
public static class TestFixtureSwitches
{
    public static async Task ApplyAsync(VsrDbContext context, CancellationToken cancellationToken = default)
    {
        foreach (var source in await context.Sources.Where(s => s.Category == "ContractBoard").ToListAsync(cancellationToken))
        {
            source.PolicyStatus = "Active";
        }

        foreach (var shift in await context.Schedules.ToListAsync(cancellationToken))
        {
            shift.IsEnabled = true;
            shift.NextRunAtUtc = DateTime.UtcNow.AddMinutes(1);
        }

        context.Sources.Add(new SourceDefinition
        {
            Id = "SRC-T01", Name = TestFixtureData.NoAdapterSource, Description = "Test-only source with no reader.",
            Category = "Careers", PolicyStatus = "Active", AllowsDirectFetch = true, DailyRequestLimit = 100
        });

        context.SearchProfiles.Add(new SearchProfile
        {
            Id = "PRF-002", Name = "React & Full-Stack Frontend Contracts",
            TargetKeywords = "React, TypeScript, Next.js, Frontend Architecture", ExcludedKeywords = "junior, student",
            Location = "Remote", OpportunityType = "Individual developer", PublishedWithinDays = 14,
            SelectedSources = new List<string> { TestFixtureData.NoAdapterSource }, MaxResults = 40, IsActive = true, CreatedBy = "System"
        });

        context.Schedules.Add(new AutomationSchedule
        {
            Id = "SCHED-002", Name = "Weekly scan", Description = "Test-only weekly schedule", Frequency = "Weekly",
            CronExpression = "0 9 * * 1", Timezone = "Asia/Kolkata", IsEnabled = false, ProfileId = "PRF-002",
            SelectedSources = new List<string> { TestFixtureData.NoAdapterSource }, RunCap = 30,
            QuietHoursStart = "22:00", QuietHoursEnd = "07:00", NextRunAtUtc = DateTime.UtcNow.AddDays(1),
            LastRunStatus = "Ready", CreatedBy = "System"
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
