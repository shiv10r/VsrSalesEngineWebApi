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

public class AgentRelayTests
{
    private const string HackerNews = "Hacker News (Freelance & Who's Hiring)";

    private readonly VsrDbContext _context = TestDbContextFactory.CreateSeeded();
    private readonly AgentRunRepository _runs;
    private readonly OpportunityRepository _opportunities;
    private readonly FakeFeeds _feeds = new();
    private readonly AgentRelayService _relay;

    public AgentRelayTests()
    {
        _runs = new AgentRunRepository(_context);
        _opportunities = new OpportunityRepository(_context);
        _relay = new AgentRelayService(
            _runs,
            new SearchProfileRepository(_context),
            new SourceRegistryRepository(_context),
            _opportunities,
            new ContactRepository(_context),
            new OutreachDraftService(new OutreachTemplateRepository(_context)),
            new SuppressionRepository(_context),
            new AgentRelayOptions { StepPauseMs = 0 },
            _feeds);
    }

    private static RelayRequest Request(params string[] sources) =>
        new("SCHED-TEST", "Test shift", "PROF-001", sources.Length == 0 ? new[] { HackerNews } : sources, 50, "Manual");

    private static Opportunity Posting(string id, string company, string title, string evidence = "", string? notes = null) => new()
    {
        Id = id,
        Company = company,
        Title = title,
        Source = HackerNews,
        Location = "Remote",
        EvidenceSnippet = evidence,
        VendorRequirementNotes = notes,
        SourceUrl = $"https://example.test/{id}",
        DeduplicationKey = $"test:{id}"
    };

    private async Task<(AgentRun Run, List<AgentEvent> Events)> RunAsync(RelayRequest? request = null)
    {
        var run = await _relay.RunAsync(request ?? Request());
        Assert.NotNull(run);
        return (run, (await _runs.GetEventsAsync(0, 500, run.Id)).ToList());
    }

    [Fact]
    public async Task Filter_RejectsCopywriterAndCustomerServicePostings_WithReasons()
    {
        _feeds.Postings = new[]
        {
            Posting("OP-T1", "IAPWE", "Freelance Copywriter"),
            Posting("OP-T2", "ShyftOff", "Customer Service Representative"),
            Posting("OP-T3", "Northwind", "Senior .NET Contract Developer")
        };

        var (run, events) = await RunAsync();

        Assert.Equal(2, run.Rejected);
        Assert.Equal(1, run.NewLeads);
        Assert.Contains(events, e => e.AgentId == AgentIds.Filter && e.Message.Contains("'Freelance Copywriter' at IAPWE: not a software role"));
        Assert.Contains(events, e => e.AgentId == AgentIds.Filter && e.Message.Contains("Customer Service Representative"));
        Assert.Null(await _opportunities.GetByDeduplicationKeyAsync("test:OP-T1"));
        Assert.NotNull(await _opportunities.GetByDeduplicationKeyAsync("test:OP-T3"));
    }

    [Fact]
    public async Task Messages_AreBuiltFromRealData()
    {
        _feeds.Postings = new[]
        {
            Posting("OP-T1", "Northwind", "Senior .NET Contract Developer"),
            Posting("OP-T2", "Brightpath", "React Frontend Engineer")
        };

        var (_, events) = await RunAsync();

        Assert.Contains(events, e => e.AgentId == AgentIds.Scout && e.Message == $"Read 2 posting(s) from {HackerNews}.");
        Assert.Contains(events, e => e.AgentId == AgentIds.Scorer && e.Message.StartsWith("Northwind: 'Senior .NET Contract Developer' scores "));
        Assert.Contains(events, e => e.AgentId == AgentIds.Dedupe && e.Message == "2 new, 0 already stored.");
        Assert.All(events, e => Assert.False(string.IsNullOrWhiteSpace(e.Message)));
        Assert.Contains(events, e => e.Kind == EventKinds.Handoff);
    }

    [Fact]
    public async Task Events_ArePersistedInOrder_WithIncreasingIds_FromScoutToReporter()
    {
        _feeds.Postings = new[] { Posting("OP-T1", "Northwind", ".NET Contract Developer") };

        var (run, events) = await RunAsync();

        Assert.True(events.Count > 5);
        Assert.Equal(events.Select(e => e.Id).OrderBy(i => i), events.Select(e => e.Id));
        Assert.Equal(events.Count, events.Select(e => e.Id).Distinct().Count());
        Assert.All(events, e => Assert.Equal(run.Id, e.RunId));
        Assert.Equal(AgentIds.Scout, events.First().AgentId);
        Assert.Equal(AgentIds.Reporter, events.Last().AgentId);
        Assert.Equal("completed", run.Status);
        Assert.NotNull(run.FinishedAtUtc);
    }

    [Fact]
    public async Task Events_CanBePolledWithAfterId()
    {
        _feeds.Postings = new[] { Posting("OP-T1", "Northwind", ".NET Contract Developer") };
        var (run, events) = await RunAsync();

        var midpoint = events[events.Count / 2].Id;
        var later = await _runs.GetEventsAsync(midpoint, 500, run.Id);

        Assert.All(later, e => Assert.True(e.Id > midpoint));
        Assert.Equal(events.Count(e => e.Id > midpoint), later.Count);
    }

    [Fact]
    public async Task SecondRun_SkipsPostingsAlreadyStored()
    {
        _feeds.Postings = new[] { Posting("OP-T1", "Northwind", ".NET Contract Developer") };

        var (first, _) = await RunAsync();
        var (second, events) = await RunAsync();

        Assert.Equal(1, first.NewLeads);
        Assert.Equal(0, second.NewLeads);
        Assert.Equal(1, second.Duplicates);
        Assert.Contains(events, e => e.AgentId == AgentIds.Dedupe && e.Message.StartsWith("Already have '.NET Contract Developer' at Northwind"));
    }

    [Fact]
    public async Task StrongLeadWithEmail_GetsDraft_PassesCompliance_AndCreatesContact()
    {
        _feeds.Postings = new[]
        {
            Posting("OP-T1", "Northwind", "Senior .NET Contract Developer (Remote)", "Cloud API work, mail hiring@northwind.example", "Direct Founder / Hiring Email: hiring@northwind.example")
        };

        var (run, events) = await RunAsync();

        Assert.Equal(1, run.DraftsReady);
        Assert.Contains(events, e => e.AgentId == AgentIds.Drafter && e.Kind == EventKinds.Result && e.Message.Contains("Not sent"));
        Assert.Contains(events, e => e.AgentId == AgentIds.Compliance && e.Message.StartsWith("Passed for Northwind"));
        var draftEvent = events.Last(e => e.AgentId == AgentIds.Compliance && e.LeadJson != null);
        Assert.Contains("\"draft\"", draftEvent.LeadJson);
        Assert.NotNull(await new ContactRepository(_context).GetByNormalizedEmailAsync("hiring@northwind.example"));
    }

    [Fact]
    public async Task StrongLeadWithoutEmail_IsFlaggedForManualHandoff_NeverAutomated()
    {
        _feeds.Postings = new[] { Posting("OP-T1", "Northwind", "Senior .NET Contract Developer (Remote)", "Cloud API work") };

        var (_, events) = await RunAsync();

        Assert.Contains(events, e => e.AgentId == AgentIds.ContactFinder && e.Kind == EventKinds.Warning &&
                                     e.Message.Contains("manual LinkedIn handoff") && e.Message.Contains("never automate"));
    }

    [Fact]
    public async Task SuppressedRecipient_IsBlockedByCompliance()
    {
        _feeds.Postings = new[]
        {
            Posting("OP-T1", "Prospect Org", "Senior .NET Contract Developer (Remote)", "Cloud API work", "Email: do-not-contact@prospect.org")
        };

        var (run, events) = await RunAsync();

        Assert.Equal(0, run.DraftsReady);
        Assert.Contains(events, e => e.AgentId == AgentIds.Compliance && e.Kind == EventKinds.Warning &&
                                     e.Message.Contains("suppression list"));
    }

    [Fact]
    public async Task WhenEverySourceFails_RunIsMarkedFailed_AndNothingIsInvented()
    {
        _feeds.Throw = true;

        var (run, events) = await RunAsync();

        Assert.Equal("failed", run.Status);
        Assert.Equal(0, run.NewLeads);
        Assert.Contains(events, e => e.AgentId == AgentIds.Scout && e.Kind == EventKinds.Warning && e.Message.StartsWith($"Could not read {HackerNews}"));
    }

    [Fact]
    public async Task Source_WithoutLiveReader_IsSkippedWithoutFakeData()
    {
        var (_, events) = await RunAsync(Request("Company careers"));

        Assert.Contains(events, e => e.AgentId == AgentIds.Scout && e.Message.Contains("no live reader") && e.Message.Contains("won't make postings up"));
        Assert.Equal(0, await _context.Opportunities.CountAsync(o => o.Source == "Company careers" && o.RetrievedAtUtc > DateTime.UtcNow.AddMinutes(-1)));
    }

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
        var busy = new AgentActivityController(Activity(), new FakeLauncher(null)) { ControllerContext = WithHttpContext() };
        var started = new AgentActivityController(Activity(), new FakeLauncher("RUN-1")) { ControllerContext = WithHttpContext() };

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
        Assert.Equal(8, overview.Agents.Count);
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
            var result = ScoringRules.Score(sample, keywords, KeywordText.FindEmail(sample.VendorRequirementNotes) != null);
            var sum = result.Reasons.Sum(r => int.Parse(Regex.Match(r, @"\(\+(\d+)\)$").Groups[1].Value));
            Assert.Equal(sum, result.Score);
        }

        var strong = ScoringRules.Score(samples[0], keywords, true);
        Assert.Contains(strong.Reasons, r => r == "Contract engagement (+20)");
        Assert.Contains(strong.Reasons, r => r == ".NET / C# match (+25)");
        Assert.Contains(strong.Reasons, r => r == "Public contact email in the posting (+15)");
        Assert.Equal("High", strong.Fit);
    }

    private static ControllerContext WithHttpContext() =>
        new() { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

    private AgentActivityService Activity() => new(_runs, new AutomationScheduleRepository(_context));

    private sealed class FakeLauncher : IManualRunLauncher
    {
        private readonly string? _runId;
        public FakeLauncher(string? runId) => _runId = runId;
        public Task<string?> StartAsync(string user) => Task.FromResult(_runId);
    }

    private sealed class FakeFeeds : ILiveJobFeedService
    {
        public IReadOnlyList<Opportunity> Postings { get; set; } = Array.Empty<Opportunity>();
        public bool Throw { get; set; }

        public Task<IReadOnlyList<Opportunity>> FetchAllLiveContractJobsAsync(string? keywords = null, IEnumerable<string>? sources = null, int maxResults = 30, CancellationToken cancellationToken = default)
            => Throw ? throw new HttpRequestException("feed unreachable") : Task.FromResult(Postings);

        public Task<IReadOnlyList<Opportunity>> FetchHackerNewsContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Opportunity>> FetchWeWorkRemotelyContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Opportunity>> FetchRemotiveContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Opportunity>> FetchJobicyContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
