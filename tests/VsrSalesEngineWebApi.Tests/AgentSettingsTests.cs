using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VsrSalesEngineWebApi.Controllers;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Core.Services.AgentRelay;
using VsrSalesEngineWebApi.Infrastructure.Data;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using VsrSalesEngineWebApi.Security;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;
public class AgentSettingsTests : AgentSettingsTestBase
{
    // ---- reading ----

    [Fact]
    public async Task Get_ReturnsDefaults_ForAFreshDatabase()
    {
        var s = await _settings.GetAsync();

        Assert.Equal(AgentCatalog.All.Select(a => a.Id), s.Agents.Select(a => a.Id));
        Assert.All(s.Agents, a => Assert.Equal(AgentCatalog.NameOf(a.Id), a.Name));
        Assert.All(s.Agents, a => Assert.Equal(a.Name, a.DefaultName));
        Assert.Equal(new[] { "scout", "dedupe", "reporter" }, s.Agents.Where(a => a.Required).Select(a => a.Id).OrderBy(i => i == "scout" ? 0 : i == "dedupe" ? 1 : 2));
        Assert.Equal(50, s.DraftThreshold);
        Assert.NotEmpty(s.Keywords.Target);
        Assert.Equal(3, s.Shifts.Count);
        Assert.All(s.Shifts, x => Assert.Equal(2, x.IntervalHours));
        Assert.Equal(4, s.AvailableSources.Count);
        Assert.Null(s.UpdatedBy);
    }

    // ---- saving ----

    [Fact]
    public async Task Save_PersistsEverything_AndRoundTrips()
    {
        var u = WithAgent(await CurrentAsync(), "scout", name: "Asha");
        u = WithAgent(u, "drafter", enabled: false);
        u = u with
        {
            DraftThreshold = 60,
            Keywords = new UpdateKeywordsDto(new List<string> { "React", "Node.js" }, new List<string> { "intern" })
        };
        u = WithShift(u, s => s with { IntervalHours = 3, Sources = new List<string> { HackerNews.ToLowerInvariant() }, ActiveFrom = "08:00", ActiveTo = "20:30" });

        var result = await SaveAsync(u);

        Assert.True(result.Ok);
        var s = await _settings.GetAsync();
        Assert.Equal("Asha", s.Agents.Single(a => a.Id == "scout").Name);
        Assert.False(s.Agents.Single(a => a.Id == "drafter").Enabled);
        Assert.Equal(60, s.DraftThreshold);
        Assert.Equal(new[] { "React", "Node.js" }, s.Keywords.Target);
        Assert.Equal(new[] { "intern" }, s.Keywords.Excluded);
        Assert.Equal(3, s.Shifts[0].IntervalHours);
        Assert.Equal(new[] { HackerNews }, s.Shifts[0].Sources);
        Assert.Equal("08:00", s.Shifts[0].ActiveFrom);
        Assert.Equal("20:30", s.Shifts[0].ActiveTo);
        Assert.Equal(User, s.UpdatedBy);
        Assert.NotNull(s.UpdatedAtUtc);

        var schedule = (await new AutomationScheduleRepository(_context).GetByIdAsync(s.Shifts[0].ScheduleId))!;
        Assert.Equal("20:30", schedule.QuietHoursStart);
        Assert.Equal("08:00", schedule.QuietHoursEnd);
    }

    [Fact]
    public async Task Save_WritesOneFeedEvent_NamingWhoChangedWhat()
    {
        var u = (await CurrentAsync()) with { DraftThreshold = 60 };
        u = WithShift(u, s => s with { IntervalHours = 3 });

        await SaveAsync(u);

        var e = Assert.Single(await _runs.GetEventsAsync(0, 50, AgentSettingsService.SettingsEventRunId));
        Assert.Equal(AgentIds.Reporter, e.AgentId);
        Assert.Equal(EventKinds.Result, e.Kind);
        Assert.StartsWith($"Settings changed by {User}: Draft threshold 50 -> 60", e.Message);
        Assert.Contains("every 2h -> 3h", e.Message);
    }

    [Fact]
    public async Task Save_WithNoChanges_WritesNoEvent()
    {
        await SaveAsync(await CurrentAsync());

        Assert.Empty(await _runs.GetEventsAsync(0, 50, AgentSettingsService.SettingsEventRunId));
    }

    // ---- the relay obeys the settings ----

    [Fact]
    public async Task Filter_Off_KeepsEveryPosting()
    {
        _feeds.Postings = new[] { Posting("OP-1", "IAPWE", "Freelance Copywriter"), StrongLead() };
        await SaveAsync(WithAgent(await CurrentAsync(), "filter", enabled: false));

        var (run, events) = await RunAsync();

        Assert.Equal(0, run.Rejected);
        Assert.Equal(2, run.NewLeads);
        Assert.Contains(events, e => e.AgentId == AgentIds.Filter && e.Message == "Priya is switched off - skipping this step. Keeping every posting.");
    }

    [Fact]
    public async Task Scorer_Off_SavesLeadsWithoutScores_AndNothingIsDrafted()
    {
        _feeds.Postings = new[] { StrongLead() };
        await SaveAsync(WithAgent(await CurrentAsync(), "scorer", enabled: false));

        var (run, events) = await RunAsync();

        Assert.Equal(1, run.NewLeads);
        Assert.Equal(0, run.DraftsReady);
        Assert.Contains(events, e => e.AgentId == AgentIds.Scorer && e.Message == "Ananya is switched off - skipping this step. Saved 1 lead(s) without scores, so none will be drafted.");
        Assert.DoesNotContain(events, e => e.AgentId == AgentIds.Scorer && e.Message.Contains("scores "));
        Assert.NotNull(await new OpportunityRepository(_context).GetByDeduplicationKeyAsync("test:OP-S1"));
    }

    [Fact]
    public async Task ContactFinder_Off_SkipsTheContactStep()
    {
        _feeds.Postings = new[] { StrongLead() };
        await SaveAsync(WithAgent(await CurrentAsync(), "contact-finder", enabled: false));

        var (run, events) = await RunAsync();

        Assert.Equal("completed", run.Status);
        Assert.Contains(events, e => e.AgentId == AgentIds.ContactFinder && e.Message == "Rohan is switched off - skipping this step. No contact search this run.");
        Assert.Null(await new ContactRepository(_context).GetByNormalizedEmailAsync("hiring@northwind.example"));
    }

    [Fact]
    public async Task Drafter_Off_MakesNoDrafts()
    {
        _feeds.Postings = new[] { StrongLead() };
        var u = WithAgent(await CurrentAsync(), "drafter", enabled: false);
        await SaveAsync(WithAgent(u, "compliance", enabled: false));

        var (run, events) = await RunAsync();

        Assert.Equal(0, run.DraftsReady);
        Assert.Contains(events, e => e.AgentId == AgentIds.Drafter && e.Message == "Sophie is switched off - skipping this step. No drafts this run.");
        Assert.Contains(events, e => e.AgentId == AgentIds.Compliance && e.Message.StartsWith("Meera is switched off"));
    }

    [Fact]
    public async Task RequiredAgents_AlwaysRun_EvenIfStoredAsOff()
    {
        _context.AgentSettings.Add(new AgentSettings { AgentsJson = "[{\"id\":\"scout\",\"name\":\"Aarav\",\"enabled\":false}]" });
        await _context.SaveChangesAsync();
        _feeds.Postings = new[] { StrongLead() };

        var (run, events) = await RunAsync();

        Assert.Equal("completed", run.Status);
        Assert.DoesNotContain(events, e => e.Message.Contains("switched off"));
    }

    [Fact]
    public async Task RenamedAgents_AreNamedInEveryMessage_AndOnTheCards()
    {
        _feeds.Postings = new[] { StrongLead() };
        await SaveAsync(WithAgent(await CurrentAsync(), "filter", name: "Asha"));

        var (_, events) = await RunAsync();

        Assert.Contains("Asha", events.First(e => e.AgentId == AgentIds.Scout && e.Kind == EventKinds.Handoff).Message);
        Assert.DoesNotContain(events, e => e.Message.Contains("Priya"));
        var card = (await _activity.GetOverviewAsync()).Agents.Single(a => a.Id == "filter");
        Assert.Equal("Asha", card.Name);
    }

    [Fact]
    public async Task DraftThreshold_ComesFromTheSavedSettings()
    {
        _feeds.Postings = new[] { StrongLead() };
        await SaveAsync((await CurrentAsync()) with { DraftThreshold = 100 });

        var (run, events) = await RunAsync();

        Assert.Equal(0, run.DraftsReady);
        Assert.Contains(events, e => e.AgentId == AgentIds.Drafter && e.Message.Contains("No lead scored above 100"));
    }

    [Fact]
    public async Task Overview_ShowsSwitchedOffAgentsAsIdle()
    {
        await SaveAsync(WithAgent(await CurrentAsync(), "drafter", enabled: false));

        var card = (await _activity.GetOverviewAsync()).Agents.Single(a => a.Id == "drafter");

        Assert.Equal("idle", card.Status);
        Assert.Equal("Switched off in settings.", card.CurrentTask);
    }

    // ---- HTTP surface ----

    [Fact]
    public async Task Controller_Put_ReturnsOkWithSettings_Or400WithFieldErrors()
    {
        var controller = new AgentSettingsController(_settings)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, User) }, "test")) } }
        };

        var ok = Assert.IsType<OkObjectResult>(await controller.Put((await CurrentAsync()) with { DraftThreshold = 70 }));
        Assert.Equal(70, Assert.IsType<AgentSettingsDto>(ok.Value).DraftThreshold);
        Assert.Equal(User, (await _settings.GetAsync()).UpdatedBy);

        var bad = Assert.IsType<BadRequestObjectResult>(await controller.Put((await CurrentAsync()) with { DraftThreshold = 500 }));
        Assert.Contains(Assert.IsType<SettingsErrorsDto>(bad.Value).Errors, e => e.Field == "draftThreshold");
        Assert.IsType<OkObjectResult>(await controller.Get());
    }

    [Fact]
    public void Put_IsBlockedForGuests()
    {
        var put = typeof(AgentSettingsController).GetMethod(nameof(AgentSettingsController.Put))!;

        Assert.True(put.IsDefined(typeof(DisallowGuestAttribute), inherit: true));
    }

}
