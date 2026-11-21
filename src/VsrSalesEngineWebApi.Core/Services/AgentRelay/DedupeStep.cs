using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Step 3: removes postings already stored (or repeated inside this run).</summary>
public class DedupeStep : IRelayStep
{
    private const string Agent = AgentIds.Dedupe;
    public string AgentId => Agent;

    private readonly IOpportunityRepository _opportunities;

    public DedupeStep(IOpportunityRepository opportunities)
    {
        _opportunities = opportunities;
    }

    public async Task RunAsync(RelayState state, CancellationToken cancellationToken)
    {
        var log = state.Log;
        var incoming = state.Leads.ToList();

        if (incoming.Count == 0)
        {
            await log.ResultAsync(Agent, "Nothing to compare: no software postings reached me.");
            return;
        }

        await log.ThoughtAsync(Agent, $"Comparing {incoming.Count} posting(s) with what is already stored.");

        var seenThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lead in incoming)
        {
            var key = lead.Opportunity.DeduplicationKey;
            var alreadyStored = !seenThisRun.Add(key) ||
                                await _opportunities.GetByDeduplicationKeyAsync(key, cancellationToken) != null;
            if (!alreadyStored) continue;

            state.Leads.Remove(lead);
            state.Duplicates++;
            await log.ActionAsync(Agent,
                $"Already have '{KeywordText.Short(lead.Opportunity.Title, 70)}' at {lead.Opportunity.Company}. Skipping.",
                lead.ToDto());
        }

        await log.ResultAsync(Agent, $"{state.Leads.Count} new, {state.Duplicates} already stored.");
        await log.HandoffAsync(Agent, state.Leads.Count == 0
            ? "Nothing new to pass on."
            : $"Handing {state.Leads.Count} new lead(s) to {state.NameOf(AgentIds.Scorer)} for scoring.");
    }
}
