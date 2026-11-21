using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Step 4: scores each new lead with the transparent rules, then saves it.</summary>
public class ScorerStep : IRelayStep
{
    private const string Agent = AgentIds.Scorer;
    public string AgentId => Agent;

    private readonly IOpportunityRepository _opportunities;

    public ScorerStep(IOpportunityRepository opportunities)
    {
        _opportunities = opportunities;
    }

    public async Task RunAsync(RelayState state, CancellationToken cancellationToken)
    {
        var log = state.Log;
        if (state.Leads.Count == 0)
        {
            await log.ResultAsync(Agent, "No new leads to score this time.");
            return;
        }

        await log.ThoughtAsync(Agent,
            $"Scoring {state.Leads.Count} new lead(s). Points come from listed rules only: contract work, .NET match, modern stack, your keywords, a public email, remote.");

        foreach (var lead in state.Leads)
        {
            var email = KeywordText.FindEmail(lead.Opportunity.VendorRequirementNotes, lead.Opportunity.EvidenceSnippet);
            var result = ScoringRules.Score(lead.Opportunity, state.TargetKeywords, email != null);

            lead.Score = result.Score;
            lead.Reasons = result.Reasons;
            lead.Email = email;
            lead.Opportunity.Fit = result.Fit;

            await _opportunities.AddAsync(lead.Opportunity, cancellationToken);
            state.NewLeads++;
            if (result.Score > state.DraftThreshold) state.StrongLeads++;

            var why = result.Reasons.Count == 0 ? "no scoring rule matched" : string.Join(", ", result.Reasons);
            await log.ResultAsync(Agent,
                $"{lead.Opportunity.Company}: '{KeywordText.Short(lead.Opportunity.Title, 60)}' scores {result.Score}/100. {why}.",
                lead.ToDto(includeScore: true));
        }

        state.Leads.Sort((a, b) => b.Score.CompareTo(a.Score));
        await log.ResultAsync(Agent, $"Saved {state.NewLeads} new lead(s). {state.StrongLeads} score above {state.DraftThreshold}.");
        await log.HandoffAsync(Agent, $"Handing {state.Leads.Count} scored lead(s) to {state.NameOf(AgentIds.ContactFinder)}.");
    }

    /// <summary>Used when the owner switched the scorer off: leads are still saved, with no score, so none gets a draft.</summary>
    public async Task<int> SaveUnscoredAsync(RelayState state, CancellationToken cancellationToken)
    {
        foreach (var lead in state.Leads)
        {
            await _opportunities.AddAsync(lead.Opportunity, cancellationToken);
            state.NewLeads++;
        }

        return state.Leads.Count;
    }
}
