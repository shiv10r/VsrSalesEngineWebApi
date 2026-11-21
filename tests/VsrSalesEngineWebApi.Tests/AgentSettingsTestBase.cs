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
public abstract class AgentSettingsTestBase
{
    protected const string HackerNews = "Hacker News (Freelance & Who's Hiring)";
    protected const string User = "sales@test.example";

    protected readonly VsrDbContext _context = TestDbContextFactory.CreateSeeded();
    protected readonly AgentRunRepository _runs;
    protected readonly AgentSettingsService _settings;
    protected readonly AgentRelayService _relay;
    protected readonly AgentActivityService _activity;
    protected readonly FakeFeeds _feeds = new();

    protected AgentSettingsTestBase()
    {
        _runs = new AgentRunRepository(_context);
        var options = new AgentRelayOptions { StepPauseMs = 0 };
        var profiles = new SearchProfileRepository(_context);
        var schedules = new AutomationScheduleRepository(_context);
        var sources = new SourceRegistryRepository(_context);

        _settings = new AgentSettingsService(new AgentSettingsRepository(_context), profiles, schedules, sources, _runs, options);
        _relay = new AgentRelayService(_runs, profiles, sources, new OpportunityRepository(_context),
            new ContactRepository(_context), new OutreachDraftService(new OutreachTemplateRepository(_context)),
            new SuppressionRepository(_context), options, _feeds, _settings);
        _activity = new AgentActivityService(_runs, schedules, _settings);
    }

    protected static UpdateAgentSettingsDto ToUpdate(AgentSettingsDto s) => new(
        s.Agents.Select(a => new UpdateAgentDto(a.Id, a.Name, a.Enabled)).ToList(),
        s.DraftThreshold,
        new UpdateKeywordsDto(s.Keywords.Target.ToList(), s.Keywords.Excluded.ToList()),
        s.Shifts.Select(x => new UpdateShiftDto(x.ScheduleId, x.Enabled, x.IntervalHours, x.Sources.ToList(), x.ActiveFrom, x.ActiveTo)).ToList());

    protected async Task<UpdateAgentSettingsDto> CurrentAsync() => ToUpdate(await _settings.GetAsync());

    protected static UpdateAgentSettingsDto WithAgent(UpdateAgentSettingsDto u, string id, string? name = null, bool? enabled = null)
    {
        var agents = u.Agents!.Select(a => a.Id == id ? a with { Name = name ?? a.Name, Enabled = enabled ?? a.Enabled } : a).ToList();
        return u with { Agents = agents };
    }

    protected static UpdateAgentSettingsDto WithShift(UpdateAgentSettingsDto u, Func<UpdateShiftDto, UpdateShiftDto> change) =>
        u with { Shifts = u.Shifts!.Select((s, i) => i == 0 ? change(s) : s).ToList() };

    protected async Task<SettingsSaveResult> SaveAsync(UpdateAgentSettingsDto dto) => await _settings.SaveAsync(dto, User);

    protected static Opportunity Posting(string id, string company, string title, string? notes = null, string evidence = "") => new()
    {
        Id = id, Company = company, Title = title, Source = HackerNews, Location = "Remote", EvidenceSnippet = evidence,
        VendorRequirementNotes = notes, SourceUrl = $"https://example.test/{id}", DeduplicationKey = $"test:{id}"
    };

    protected static Opportunity StrongLead() => Posting("OP-S1", "Northwind", "Senior .NET Contract Developer (Remote)",
        "Direct Founder / Hiring Email: hiring@northwind.example", "Cloud API work, mail hiring@northwind.example");

    protected async Task<(AgentRun Run, List<AgentEvent> Events)> RunAsync()
    {
        var request = new RelayRequest("SCHED-TEST", "Test shift", "PROF-001", new[] { HackerNews }, 50, "Manual");
        var run = await _relay.RunAsync(request);
        Assert.NotNull(run);
        return (run, (await _runs.GetEventsAsync(0, 500, run.Id)).ToList());
    }


    protected sealed class FakeFeeds : ILiveJobFeedService
    {
        public IReadOnlyList<Opportunity> Postings { get; set; } = Array.Empty<Opportunity>();

        public Task<IReadOnlyList<Opportunity>> FetchAllLiveContractJobsAsync(string? keywords = null, IEnumerable<string>? sources = null, int maxResults = 30, CancellationToken cancellationToken = default)
            => Task.FromResult(Postings);

        public Task<IReadOnlyList<Opportunity>> FetchHackerNewsContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Opportunity>> FetchWeWorkRemotelyContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Opportunity>> FetchRemotiveContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Opportunity>> FetchJobicyContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
