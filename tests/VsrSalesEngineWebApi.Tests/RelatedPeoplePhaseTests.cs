using System.Text.Json;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Core.Services.AgentRelay;
using VsrSalesEngineWebApi.Core.Services.AgentRelay.People;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class RelatedPeoplePhaseTests : AgentSettingsTestBase
{
    private readonly FakePageFetcher _pages = new();
    private readonly ContactRepository _contacts;
    private readonly AgentRelayService _peopleRelay;

    public RelatedPeoplePhaseTests()
    {
        _contacts = new ContactRepository(_context);
        _peopleRelay = new AgentRelayService(_runs, new SearchProfileRepository(_context), new SourceRegistryRepository(_context),
            new OpportunityRepository(_context), _contacts, new AgentRelayOptions { StepPauseMs = 0 }, _feeds, _settings, new RelatedPeopleFinder(_pages));
    }

    private static Opportunity HnPost(string id, string company, string? companyUrl = "https://acme.io/", string? text = null)
    {
        var op = Posting(id, company, "Senior .NET Contract Developer");
        op.SourceUrl = $"https://news.ycombinator.com/item?id={id}";
        op.CompanyUrl = companyUrl;
        op.PosterHandle = "jsmith";
        op.PosterProfileUrl = "https://news.ycombinator.com/user?id=jsmith";
        op.PostingText = text;
        return op;
    }

    private RelatedPeoplePhase Phase(IRelatedPeopleFinder? finder = null, int maxSiteLookups = 10) =>
        new(finder ?? new RelatedPeopleFinder(_pages), _contacts, maxSiteLookups);

    private async Task<(RelayState State, AgentRun Run)> NewStateAsync(params (Opportunity Opp, int Score)[] leads)
    {
        var request = new RelayRequest("SCHED-TEST", "Test shift", "PROF-001", new[] { HackerNews }, 50, "Manual");
        var run = await _runs.AddRunAsync(new AgentRun
        {
            Id = $"RUN-T-{Guid.NewGuid().ToString("N")[..12]}", ScheduleId = "SCHED-TEST", ShiftName = "Test shift",
            Trigger = "Manual", StartedAtUtc = DateTime.UtcNow, Status = "running"
        });
        var state = new RelayState(run, request, new AgentLog(_runs, run.Id, 0), new AgentRelayOptions { StepPauseMs = 0 });
        foreach (var (opp, score) in leads) state.Leads.Add(new RelayLead(opp) { Score = score });
        return (state, run);
    }

    private async Task<List<AgentEvent>> FinderEventsAsync(string runId) =>
        (await _runs.GetEventsAsync(0, 1000, runId)).Where(e => e.AgentId == AgentIds.ContactFinder).ToList();

    private static RelatedPeopleResult Site(SiteOutcome outcome, string host, int pages = 0, string? detail = null) =>
        new(Array.Empty<RelatedPerson>(), outcome, host, pages, detail);

    // ---- narration and saved contacts ---------------------------------------------------------------------------------

    private async Task<(AgentRun Run, Opportunity Op)> RunAcmeAsync()
    {
        var op = HnPost("77", "Acme", text: "Acme | Senior .NET Contract Developer | Remote\nContact Jane Roy for details.");
        _pages.Html("https://acme.io/team", "<div><h3>Priya Sharma</h3><p>CTO</p></div>")
              .Html("https://acme.io/careers", "<p>Meera Jain, Technical Recruiter</p>");
        var (state, run) = await NewStateAsync((op, 80));
        await Phase().RunAsync(state, default);
        return (run, op);
    }

    [Fact]
    public async Task EachRelationIsNarratedPlainlyWithWhereItWasFound()
    {
        var (run, _) = await RunAcmeAsync();
        var messages = (await FinderEventsAsync(run.Id)).Select(e => e.Message).ToList();

        Assert.Contains(messages, m => m.StartsWith("jsmith posted this (Hacker News comment) - noting them as the poster."));
        Assert.Contains(messages, m => m.StartsWith("The post names Jane Roy (") && m.Contains("noting them as mentioned in the post."));
        Assert.Contains(messages, m => m.StartsWith("Found Priya Sharma, CTO, on acme.io/team (medium confidence) - a decision-maker."));
        Assert.Contains(messages, m => m.StartsWith("Found Meera Jain, Technical Recruiter, on acme.io/careers (high confidence) - a hiring contact."));
        Assert.Contains(messages, m => m.StartsWith("Noted 4 connected person(s) across 1 of 1 lead(s). 1 company site(s) checked."));
    }

    [Fact]
    public async Task EveryFoundPersonIsSavedWithRelationSourceAndNoInventedEmail()
    {
        var (_, op) = await RunAcmeAsync();
        var saved = (await _contacts.GetAllAsync()).Where(c => c.Company == "Acme").ToList();

        Assert.Equal(new[] { "decision-maker", "hiring", "mentioned", "poster" }, saved.Select(c => c.RelationToPost!).OrderBy(x => x).ToArray());

        var priya = saved.Single(c => c.Name == "Priya Sharma");
        Assert.Equal("Company website", priya.Source);
        Assert.Equal("https://acme.io/team", priya.SourceUrl);
        Assert.Equal("CTO", priya.Role);
        Assert.Equal(string.Empty, priya.Email);
        Assert.Equal("ManualHandoff", priya.ChannelBasis);
        Assert.Contains("retrieved", priya.Notes);
        Assert.Contains("medium confidence", priya.Notes);

        var poster = saved.Single(c => c.Name == "jsmith");
        Assert.Equal(op.Source, poster.Source);
        Assert.Equal(op.SourceUrl, poster.SourceUrl);
        Assert.Contains("real name is not known", poster.Notes);
        Assert.Contains("https://news.ycombinator.com/user?id=jsmith", poster.Notes);

        var jane = saved.Single(c => c.Name == "Jane Roy");
        Assert.Contains("Contact Jane Roy for details.", jane.Notes);
        Assert.All(saved, c => Assert.Equal(string.Empty, c.Email));
    }

    [Fact]
    public async Task APersonAlreadySavedIsNotSavedAgain()
    {
        await RunAcmeAsync();
        var before = (await _contacts.GetAllAsync()).Count;

        var op = HnPost("77", "Acme", text: "Acme | Senior .NET Contract Developer | Remote\nContact Jane Roy for details.");
        var (state, run) = await NewStateAsync((op, 80));
        await Phase().RunAsync(state, default);

        Assert.Equal(before, (await _contacts.GetAllAsync()).Count);
        var messages = (await FinderEventsAsync(run.Id)).Select(e => e.Message).ToList();
        Assert.Contains(messages, m => m.StartsWith("Found Priya Sharma") && m.EndsWith("Already in Contacts."));
    }

    [Fact]
    public async Task RelationToPostIsReturnedAsCamelCaseInContactDtos()
    {
        await RunAcmeAsync();
        var service = new ContactService(_contacts);
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var poster = (await _contacts.GetAllAsync()).Single(c => c.Name == "jsmith");
        var json = JsonSerializer.Serialize(await service.GetContactByIdAsync(poster.Id), web);
        Assert.Contains("\"relationToPost\":\"poster\"", json);

        var older = (await _contacts.GetAllAsync()).First(c => c.RelationToPost == null);
        var olderJson = JsonSerializer.Serialize(await service.GetContactByIdAsync(older.Id), web);
        Assert.Contains("\"relationToPost\":null", olderJson);                  // null when unknown
    }

    // ---- limits, failures and outcomes ---------------------------------------------------------------------------------

    [Fact]
    public async Task OnlyTheStrongestCompaniesAreReadAndTheRestAreCounted()
    {
        _pages.Html("https://a.io/team", "<p>Jane Roy, CTO</p>").Html("https://b.io/team", "<p>Bob Lane, CEO</p>").Html("https://c.io/team", "<p>Cy Dunn, CEO</p>");
        var (state, run) = await NewStateAsync(
            (HnPost("1", "A Co", "https://a.io/"), 90), (HnPost("2", "B Co", "https://b.io/"), 70), (HnPost("3", "C Co", "https://c.io/"), 50));

        await Phase(maxSiteLookups: 1).RunAsync(state, default);

        Assert.All(_pages.Requests, r => Assert.StartsWith("https://a.io/", r));
        var messages = (await FinderEventsAsync(run.Id)).Select(e => e.Message).ToList();
        Assert.Contains(messages, m => m.Contains("2 lead(s) were not checked on their own site this run (limit 1 companies)."));
    }

    [Fact]
    public async Task AFailingLookupIsReportedAndTheNextLeadIsStillHandled()
    {
        var finder = new ScriptedFinder((op, _) => op.Company == "Bad Co"
            ? throw new InvalidOperationException("boom")
            : new RelatedPeopleResult(new[] { new RelatedPerson("Jane Roy", "CTO", PersonRelation.DecisionMaker, "https://good.io/team", "high", null, "Jane Roy, CTO") },
                SiteOutcome.Found, "good.io", 1));
        var (state, run) = await NewStateAsync((HnPost("1", "Bad Co", "https://bad.io/"), 90), (HnPost("2", "Good Co", "https://good.io/"), 70));

        await Phase(finder).RunAsync(state, default);

        var events = await FinderEventsAsync(run.Id);
        Assert.Contains(events, e => e.Kind == "warning" && e.Message.StartsWith("Could not look up people for Bad Co"));
        Assert.Contains(await _contacts.GetAllAsync(), c => c.Name == "Jane Roy" && c.Company == "Good Co");
    }

    [Fact]
    public async Task SiteOutcomesWithoutPeopleAreExplainedInPlainWords()
    {
        var results = new Dictionary<string, RelatedPeopleResult>
        {
            ["No Pages Co"] = Site(SiteOutcome.NoPages, "nopages.io"),
            ["Robots Co"] = Site(SiteOutcome.RobotsDisallowed, "robots.io"),
            ["Unreadable Co"] = Site(SiteOutcome.RobotsDisallowed, "unreadable.io", detail: "robots.txt could not be read"),
            ["Empty Co"] = Site(SiteOutcome.NoPeopleFound, "empty.io", pages: 2),
            ["Failed Co"] = Site(SiteOutcome.FetchFailed, "failed.io")
        };
        var finder = new ScriptedFinder((op, _) => results[op.Company]);
        var leads = results.Keys.Select((c, i) => (HnPost($"{i}", c, $"https://{c.Split(' ')[0].ToLower()}.io/"), 50)).ToArray();
        var (state, run) = await NewStateAsync(leads);

        await Phase(finder).RunAsync(state, default);

        var events = await FinderEventsAsync(run.Id);
        Assert.Contains(events, e => e.Kind == "result" && e.Message == "No team or careers page on nopages.io - leaving this one for manual research.");
        Assert.Contains(events, e => e.Kind == "warning" && e.Message.StartsWith("robots.io asks bots not to read its team pages (robots.txt), so I skipped it."));
        Assert.Contains(events, e => e.Kind == "warning" && e.Message.StartsWith("Could not read robots.txt on unreadable.io"));
        Assert.Contains(events, e => e.Message.StartsWith("Read 2 page(s) on empty.io but found nobody with a relevant title"));
        Assert.Contains(events, e => e.Kind == "warning" && e.Message.StartsWith("Could not load failed.io"));
    }

    // ---- inside a full run ----------------------------------------------------------------------------------------------

    private async Task<List<AgentEvent>> RunRelayAsync()
    {
        var request = new RelayRequest("SCHED-TEST", "Test shift", "PROF-001", new[] { HackerNews }, 50, "Manual");
        var run = await _peopleRelay.RunAsync(request);
        Assert.NotNull(run);
        return (await _runs.GetEventsAsync(0, 1000, run.Id)).ToList();
    }

    [Fact]
    public async Task AFullRunNarratesThePosterAndSavesThemAsAContact()
    {
        _feeds.Postings = new[] { HnPost("OP-HN-5", "Acme", text: "Acme | Senior .NET Contract Developer | Remote") };

        var events = await RunRelayAsync();

        Assert.Contains(events, e => e.AgentId == AgentIds.ContactFinder && e.Message.StartsWith("jsmith posted this (Hacker News comment)"));
        Assert.Contains(await _contacts.GetAllAsync(), c => c.Name == "jsmith" && c.RelationToPost == "poster");
    }

    [Fact]
    public async Task EventsUseTheCurrentAgentNamesNotTheDefaults()
    {
        var update = await CurrentAsync();
        update = WithAgent(WithAgent(update, "contact-finder", name: "Asha"), "reporter", name: "Kavya");
        Assert.True((await SaveAsync(update)).Ok);
        _feeds.Postings = new[] { StrongLead() };

        var events = await RunRelayAsync();

        var finder = events.Where(e => e.AgentId == AgentIds.ContactFinder).ToList();
        Assert.NotEmpty(finder);
        Assert.DoesNotContain(finder, e => e.Message.Contains("Rohan"));
        Assert.Contains(finder, e => e.Kind == "handoff" && e.Message.Contains("Kavya"));
    }

    [Fact]
    public async Task ASwitchedOffContactFinderReadsNothing()
    {
        Assert.True((await SaveAsync(WithAgent(await CurrentAsync(), "contact-finder", enabled: false))).Ok);
        _feeds.Postings = new[] { HnPost("OP-HN-6", "Acme") };

        var events = await RunRelayAsync();

        Assert.Contains(events, e => e.AgentId == AgentIds.ContactFinder && e.Message.Contains("is switched off - skipping this step"));
        Assert.Empty(_pages.Requests);
        Assert.DoesNotContain(await _contacts.GetAllAsync(), c => c.Name == "jsmith");
    }
}
