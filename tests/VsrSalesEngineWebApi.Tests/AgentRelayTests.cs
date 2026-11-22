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
        EvidenceSnippet = evidence + (notes == null ? "" : " " + notes),
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
    public async Task LeadWithPublicEmail_CreatesContact_AndLeadWithoutIsLeftForManualReview()
    {
        _feeds.Postings = new[]
        {
            Posting("OP-T1", "Northwind", "Senior .NET Contract Developer (Remote)", "Cloud API work, mail hiring@northwind.example"),
            Posting("OP-T2", "Acme", "Senior .NET Contract Developer (Remote)", "Cloud API work")
        };

        var (run, events) = await RunAsync();

        Assert.Equal(2, run.NewLeads);
        Assert.NotNull(await new ContactRepository(_context).GetByNormalizedEmailAsync("hiring@northwind.example"));
        Assert.Contains(events, e => e.AgentId == AgentIds.ContactFinder && e.Message.StartsWith("1 of 2 lead(s) have a public email"));
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
        var (_, events) = await RunAsync(Request(TestFixtureData.NoAdapterSource));

        Assert.Contains(events, e => e.AgentId == AgentIds.Scout && e.Message.Contains($"no adapter for {TestFixtureData.NoAdapterSource}") && e.Message.Contains("won't make postings up"));
        Assert.Equal(0, await _context.Opportunities.CountAsync(o => o.Source == TestFixtureData.NoAdapterSource && o.RetrievedAtUtc > DateTime.UtcNow.AddMinutes(-1)));
    }


    private static ControllerContext WithHttpContext() =>
        new() { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };

    private AgentActivityService Activity() => new(_runs, new AutomationScheduleRepository(_context));

    private LeadHeatmapService Heatmap() => new(_opportunities, _runs);

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
