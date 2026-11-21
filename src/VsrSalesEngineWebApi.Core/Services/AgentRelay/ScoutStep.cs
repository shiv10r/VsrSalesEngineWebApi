using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Step 1: reads each selected live source. Never invents postings when a source is unavailable.</summary>
public class ScoutStep : IRelayStep
{
    private const string Agent = AgentIds.Scout;
    public string AgentId => Agent;

    private readonly ILiveJobFeedService? _feeds;
    private readonly ISourceRegistryRepository _sources;

    public ScoutStep(ILiveJobFeedService? feeds, ISourceRegistryRepository sources)
    {
        _feeds = feeds;
        _sources = sources;
    }

    public async Task RunAsync(RelayState state, CancellationToken cancellationToken)
    {
        var log = state.Log;
        var requested = state.Request.Sources;
        var keywords = state.Profile?.TargetKeywords;
        if (string.IsNullOrWhiteSpace(keywords)) keywords = ".NET Contractor";

        await log.ThoughtAsync(Agent,
            $"Starting {state.Request.ShiftName}. {requested.Count} source(s) on my list: {string.Join(", ", requested)}. Looking for: {keywords}.");

        var registry = (await _sources.GetAllSourcesAsync(cancellationToken))
            .ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in requested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ReadSourceAsync(state, registry, name, keywords!, cancellationToken);
        }

        await log.HandoffAsync(Agent,
            $"Read {state.PostingsRead} posting(s) from {state.SourcesReached} source(s). Handing them to {state.NameOf(AgentIds.Filter)}.");
    }

    private async Task ReadSourceAsync(
        RelayState state, Dictionary<string, SourceDefinition> registry, string name, string keywords, CancellationToken ct)
    {
        var log = state.Log;

        if (!registry.TryGetValue(name, out var definition) ||
            definition.PolicyStatus is "Disabled" or "Restricted" or "UnderReview")
        {
            var status = definition?.PolicyStatus ?? "not registered";
            await log.WarningAsync(Agent, $"Skipping {name}: its source policy is '{status}'. I only read sources marked Active.");
            state.SourcesFailed++;
            return;
        }

        if (_feeds == null || !LiveSourceNames.IsLive(name))
        {
            await log.WarningAsync(Agent, $"Skipping {name}: there is no live reader for it yet, and I won't make postings up.");
            state.SourcesFailed++;
            return;
        }

        await log.ActionAsync(Agent, $"Opening {name}...");
        await _sources.IncrementDailyRequestCountAsync(definition.Id, 1, ct);

        try
        {
            var postings = await _feeds.FetchAllLiveContractJobsAsync(keywords, new[] { name }, Math.Max(5, state.Request.RunCap), ct);
            state.PostingsRead += postings.Count;
            state.SourcesReached++;
            state.Leads.AddRange(postings.Select(p => new RelayLead(p)));

            var message = postings.Count == 0
                ? $"{name} answered but had no matching postings right now."
                : $"Read {postings.Count} posting(s) from {name}.";
            await log.ResultAsync(Agent, message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            state.SourcesFailed++;
            await log.WarningAsync(Agent, $"Could not read {name}: {KeywordText.Short(ex.Message, 120)}. Moving on.");
        }
    }
}

