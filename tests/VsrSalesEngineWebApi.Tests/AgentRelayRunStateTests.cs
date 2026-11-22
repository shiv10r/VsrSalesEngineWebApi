using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VsrSalesEngineWebApi.Controllers;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Core.Services.AgentRelay;
using VsrSalesEngineWebApi.Infrastructure.Data;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;
public partial class AgentRelayTests
{
    [Fact]
    public async Task BeginRun_ReturnsNullWhileAnotherRunIsInProgress()
    {
        var first = await _relay.BeginRunAsync(Request());
        var second = await _relay.BeginRunAsync(Request());

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task BeginRun_RecoversFromAStaleRunningRow()
    {
        _context.AgentRuns.Add(new AgentRun { Id = "RUN-OLD", StartedAtUtc = DateTime.UtcNow.AddHours(-3), Status = "running" });
        await _context.SaveChangesAsync();

        var run = await _relay.BeginRunAsync(Request());

        Assert.NotNull(run);
        Assert.Equal("failed", (await _runs.GetRunAsync("RUN-OLD"))!.Status);
    }

    [Fact]
    public async Task RunNowEndpoint_Returns409WhenBusy_And202WithRunIdOtherwise()
    {
        var busy = new AgentActivityController(Activity(), new FakeLauncher(null), Heatmap()) { ControllerContext = WithHttpContext() };
        var started = new AgentActivityController(Activity(), new FakeLauncher("RUN-1"), Heatmap()) { ControllerContext = WithHttpContext() };

        Assert.IsType<ConflictObjectResult>(await busy.RunNow());
        var accepted = Assert.IsType<AcceptedResult>(await started.RunNow());
        Assert.Equal("RUN-1", Assert.IsType<RunNowResultDto>(accepted.Value).RunId);
    }

    [Fact]
    public async Task Overview_CountsMatchStoredEvents()
    {
        _feeds.Postings = new[]
        {
            Posting("OP-T1", "Northwind", "Senior .NET Contract Developer (Remote)", "Cloud API work", "Email: hiring@northwind.example"),
            Posting("OP-T2", "IAPWE", "Freelance Writer")
        };
        var (run, events) = await RunAsync();

        var overview = await Activity().GetOverviewAsync();

        Assert.False(overview.RunInProgress);
        Assert.Equal(6, overview.Agents.Count);
        Assert.Equal(run.Id, overview.LastRun!.Id);
        foreach (var card in overview.Agents)
        {
            var mine = events.Where(e => e.AgentId == card.Id).ToList();
            Assert.Equal(mine.Count, card.Lifetime.EventsLogged);
            Assert.Equal(mine.Count(e => e.LeadJson != null), card.Lifetime.ItemsProcessed);
        }
    }

    [Fact]
    public async Task Overview_ShowsTheCurrentAgentWorking_WhileARunIsInFlight()
    {
        var run = await _relay.BeginRunAsync(Request());
        await new AgentLog(_runs, run!.Id, 0).ActionAsync(AgentIds.Scorer, "Scoring Northwind");

        var overview = await Activity().GetOverviewAsync();

        Assert.True(overview.RunInProgress);
        Assert.Equal("working", overview.Agents.Single(a => a.Id == AgentIds.Scorer).Status);
        Assert.Equal("Scoring Northwind", overview.Agents.Single(a => a.Id == AgentIds.Scorer).CurrentTask);
        Assert.Equal("idle", overview.Agents.Single(a => a.Id == AgentIds.Scout).Status);
        Assert.Equal("waiting", overview.Agents.Single(a => a.Id == AgentIds.Reporter).Status);
    }

    [Fact]
    public async Task OffShiftRun_IsRecordedAsSkippedWithReason()
    {
        await _relay.RecordSkippedAsync(Request(), "Night shift is off duty (active 22:00-06:00 Asia/Kolkata).");

        var run = (await _runs.GetRunsAsync(1)).Single();
        var events = await _runs.GetEventsAsync(0, 10, run.Id);

        Assert.Equal("skipped", run.Status);
        Assert.Single(events);
        Assert.Contains("off duty", events[0].Message);
    }

    [Fact]
    public void Catalog_UsesPersonaNames_AndKeepsIdsAndFunctionLabels()
    {
        var expected = new (string Id, string Name, string Label)[]
        {
            ("scout", "Aarav", "Scout - "),
            ("filter", "Priya", "Filter - "),
            ("dedupe", "Daniel", "Dedupe - "),
            ("scorer", "Ananya", "Scorer - "),
            ("contact-finder", "Rohan", "Contact Finder - "),
            ("reporter", "James", "Reporter - ")
        };

        Assert.Equal(expected.Select(e => e.Id), AgentCatalog.All.Select(a => a.Id));
        foreach (var (id, name, label) in expected)
        {
            var agent = AgentCatalog.All.Single(a => a.Id == id);
            Assert.Equal(name, agent.Name);
            Assert.StartsWith(label, agent.Role);
            Assert.Equal(name, AgentCatalog.NameOf(id));
        }
    }

    [Fact]
    public async Task Handoffs_NameTheNextAgentByPersona()
    {
        _feeds.Postings = new[]
        {
            Posting("OP-T1", "Northwind", "Senior .NET Contract Developer (Remote)", "Cloud API work", "Email: hiring@northwind.example")
        };

        var (_, events) = await RunAsync();

        string Handoff(string from) => events.First(e => e.AgentId == from && e.Kind == EventKinds.Handoff).Message;
        Assert.Contains("Priya", Handoff(AgentIds.Scout));
        Assert.Contains("Daniel", Handoff(AgentIds.Filter));
        Assert.Contains("Ananya", Handoff(AgentIds.Dedupe));
        Assert.Contains("Rohan", Handoff(AgentIds.Scorer));
        Assert.Contains("James", Handoff(AgentIds.ContactFinder));
    }

    [Fact]
    public async Task Overview_ShowsPersonaNamesWithStableIds()
    {
        var overview = await Activity().GetOverviewAsync();

        Assert.Equal("Aarav", overview.Agents.Single(a => a.Id == "scout").Name);
        Assert.Equal("James", overview.Agents.Single(a => a.Id == "reporter").Name);
        Assert.StartsWith("Scout - ", overview.Agents.Single(a => a.Id == "scout").Role);
    }

    [Fact]
    public void ScoringReasons_AlwaysSumToTheScore()
    {
        var keywords = new[] { "asp.net", "react" };
        var samples = new[]
        {
            Posting("A", "Co", "Senior .NET Contract Developer (Remote)", "ASP.NET API on Azure, React front end", "hr@co.example"),
            Posting("B", "Co", "React Engineer", "TypeScript"),
            Posting("C", "Co", "Freelance Copywriter")
        };

        foreach (var sample in samples)
        {
            var result = ScoringRules.Score(sample, keywords, KeywordText.FindEmail(sample.EvidenceSnippet) != null);
            var sum = result.Reasons.Sum(r => int.Parse(Regex.Match(r, @"\(\+(\d+)\)$").Groups[1].Value));
            Assert.Equal(sum, result.Score);
        }

        var strong = ScoringRules.Score(samples[0], keywords, true);
        Assert.Contains(strong.Reasons, r => r == "Contract engagement (+20)");
        Assert.Contains(strong.Reasons, r => r == ".NET / C# match (+25)");
        Assert.Contains(strong.Reasons, r => r == "Public contact email in the posting (+15)");
        Assert.Equal("High", strong.Fit);
    }
}
