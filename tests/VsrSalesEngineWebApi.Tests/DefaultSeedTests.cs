using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services.AgentRelay;
using VsrSalesEngineWebApi.Infrastructure.Data;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

/// <summary>The production seed is configuration only: no invented business data, and nothing switched on.</summary>
public class DefaultSeedTests : AgentSettingsTestBase
{
    public DefaultSeedTests() : base(TestDbContextFactory.CreateDefaultSeeded()) { }

    [Fact]
    public async Task Seed_ContainsNoInventedBusinessData()
    {
        Assert.Empty(await _context.Opportunities.ToListAsync());
        Assert.Empty(await _context.Contacts.ToListAsync());
    }

    [Fact]
    public async Task Seed_KeepsConfiguration()
    {
        Assert.Equal(3, await _context.Schedules.CountAsync());
        Assert.Single(await _context.SearchProfiles.ToListAsync());
    }

    [Fact]
    public async Task Seed_SourceRegistryHasNoFakeSourcesAndNoInventedCounters()
    {
        var sources = await _context.Sources.ToListAsync();
        var names = sources.Select(s => s.Name).ToList();

        foreach (var fake in new[] { "Company careers", "Public job board", "Direct company research", "Public procurement", "Approved feed" })
        {
            Assert.DoesNotContain(fake, names);
        }

        Assert.Contains(sources, s => s.Name == "LinkedIn handoff" && s.PolicyStatus == "Restricted");
        Assert.Equal(4, sources.Count(s => LiveSourceNames.IsLive(s.Name)));
        Assert.All(sources.Where(s => LiveSourceNames.IsLive(s.Name)), s => Assert.Equal("Disabled", s.PolicyStatus));
        Assert.All(sources, s =>
        {
            Assert.Equal(0, s.CurrentDayRequests);
            Assert.Equal("Unknown", s.HealthStatus);
        });
    }

    [Fact]
    public async Task Seed_ShiftSchedulesAreSwitchedOffAndWeeklyScanIsGone()
    {
        var schedules = await _context.Schedules.ToListAsync();

        Assert.Equal(new[] { "SCHED-001", "SCHED-003", "SCHED-004" }, schedules.Select(s => s.Id).OrderBy(i => i));
        Assert.All(schedules, s => Assert.False(s.IsEnabled));
        Assert.All(schedules, s => Assert.Null(s.LastRunAtUtc));
    }

    [Fact]
    public async Task Seed_RemovesLegacyDemoRowsFromAnExistingDatabase()
    {
        _context.Opportunities.Add(new Opportunity { Id = "OP-1001", Company = "Northstar Digital", Title = "x" });
        _context.Opportunities.Add(new Opportunity { Id = "OP-REAL", Company = "Real Co", Title = "y", DeduplicationKey = "real" });
        await _context.SaveChangesAsync();

        await VsrDbSeeder.SeedAsync(_context);

        var left = await _context.Opportunities.Select(o => o.Id).ToListAsync();
        Assert.Equal(new[] { "OP-REAL" }, left);
    }

    [Fact]
    public async Task AgentSettings_GetAndSave_WorkWhenSourcesAndShiftsAreDisabled()
    {
        var settings = await _settings.GetAsync();

        Assert.Equal(4, settings.AvailableSources.Count);
        Assert.All(settings.Shifts, s => Assert.False(s.Enabled));

        var result = await SaveAsync(ToUpdate(settings));

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Settings);
    }

    [Fact]
    public async Task Run_WithNothingEnabled_FinishesCleanlyWithHonestSummaryAndEvent()
    {
        var request = await _relay.BuildManualRequestAsync("owner");

        Assert.Empty(request.Sources);
        var run = await _relay.RunAsync(request);

        Assert.NotNull(run);
        Assert.Equal("completed", run.Status);
        Assert.Equal(0, run.NewLeads);
        Assert.Contains("had nothing to do", run.Summary);
        var events = await _runs.GetEventsAsync(0, 200, run.Id);
        Assert.Contains(events, e => e.AgentId == AgentIds.Scout && e.Message.Contains("Nothing to do"));
        Assert.Contains(events, e => e.AgentId == AgentIds.Reporter && e.Message.Contains("nothing to do"));
        Assert.Empty(await _context.Opportunities.ToListAsync());
    }

    [Fact]
    public async Task Run_WithDisabledLiveFeed_DoesNotFetchIt()
    {
        _feeds.Postings = new[] { StrongLead() };

        var run = await _relay.RunAsync(new RelayRequest("SCHED-001", "Morning shift", "PRF-001", new[] { HackerNews }, 50, "Manual"));

        Assert.NotNull(run);
        Assert.Equal("completed", run.Status);
        Assert.Contains("nothing to do", run.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await _context.Opportunities.ToListAsync());
        Assert.Equal(0, (await _context.Sources.SingleAsync(s => s.Name == HackerNews)).CurrentDayRequests);
    }
}
