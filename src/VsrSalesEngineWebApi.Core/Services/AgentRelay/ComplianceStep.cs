using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Step 7: checks every draft for an opt-out, a sender address, and the suppression list.</summary>
public class ComplianceStep : IRelayStep
{
    private const string Agent = AgentIds.Compliance;

    private readonly IOutreachDraftService _drafts;
    private readonly ISuppressionRepository _suppressions;

    public ComplianceStep(IOutreachDraftService drafts, ISuppressionRepository suppressions)
    {
        _drafts = drafts;
        _suppressions = suppressions;
    }

    public async Task RunAsync(RelayState state, CancellationToken cancellationToken)
    {
        var log = state.Log;
        var drafted = state.Leads.Where(l => l.Draft != null).ToList();

        if (drafted.Count == 0)
        {
            await log.ResultAsync(Agent, "No drafts to check.");
            return;
        }

        await log.ThoughtAsync(Agent, $"Checking {drafted.Count} draft(s): opt-out link, sender address, and the suppression list.");

        foreach (var lead in drafted)
        {
            var draft = lead.Draft!;
            var validation = _drafts.ValidateDraft(new DraftContentValidationRequest(draft.Subject, draft.Body, null));
            var suppressed = lead.Email != null && await _suppressions.IsSuppressedAsync(lead.Email);

            var problems = new List<string>();
            if (!validation.HasUnsubscribeMechanism) problems.Add("no opt-out mechanism");
            if (!validation.HasPhysicalAddress) problems.Add("no sender address");
            if (suppressed) problems.Add("recipient is on the suppression list");
            problems.AddRange(validation.ProhibitedClaims.Select(c => $"prohibited claim: {c}"));

            var company = lead.Opportunity.Company;
            if (problems.Count > 0)
            {
                lead.Draft = null;
                await log.WarningAsync(Agent,
                    $"Blocked the draft for {company}: {string.Join("; ", problems)}.",
                    lead.ToDto(includeScore: true));
                continue;
            }

            lead.CompliancePassed = true;
            state.DraftsReady++;
            var route = lead.Email == null
                ? "No public email, so this is for a manual handoff only"
                : "Recipient is not on the suppression list";
            await log.ResultAsync(Agent,
                $"Passed for {company}: opt-out present, sender address present. {route}.",
                lead.ToDto(includeScore: true, includeDraft: true));
        }

        await log.HandoffAsync(Agent, $"{state.DraftsReady} draft(s) cleared. Handing over to {AgentCatalog.NameOf(AgentIds.Reporter)}.");
    }
}
