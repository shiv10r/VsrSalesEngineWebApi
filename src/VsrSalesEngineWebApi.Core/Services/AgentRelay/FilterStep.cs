namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Step 2: drops postings that are not software work or that hit the profile's excluded keywords.</summary>
public class FilterStep : IRelayStep
{
    private const string Agent = AgentIds.Filter;
    public string AgentId => Agent;

    public async Task RunAsync(RelayState state, CancellationToken cancellationToken)
    {
        var log = state.Log;
        var incoming = state.Leads.ToList();

        if (incoming.Count == 0)
        {
            await log.ResultAsync(Agent, $"Nothing to check: {state.NameOf(AgentIds.Scout)} brought no postings.");
            return;
        }

        await log.ThoughtAsync(Agent, $"Checking {incoming.Count} posting(s) against software-role terms and {state.ExcludedKeywords.Count} excluded keyword(s).");

        foreach (var lead in incoming)
        {
            var decision = RoleFilter.Evaluate(lead.Opportunity.Title, state.ExcludedKeywords);
            if (decision.Keep) continue;

            state.Leads.Remove(lead);
            state.Rejected++;
            await log.ActionAsync(Agent,
                $"Skipped '{KeywordText.Short(lead.Opportunity.Title, 70)}' at {lead.Opportunity.Company}: {decision.Reason}.",
                lead.ToDto());
        }

        await log.ResultAsync(Agent, $"Kept {state.Leads.Count} of {incoming.Count} posting(s); dropped {state.Rejected}.");
        await log.HandoffAsync(Agent, $"Handing {state.Leads.Count} software posting(s) to {state.NameOf(AgentIds.Dedupe)}.");
    }
}
