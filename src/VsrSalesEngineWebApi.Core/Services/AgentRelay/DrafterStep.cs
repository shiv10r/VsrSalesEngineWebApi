using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Step 6: writes a grounded draft for leads above the score threshold. Nothing is ever sent from here.</summary>
public class DrafterStep : IRelayStep
{
    private const string Agent = AgentIds.Drafter;

    private readonly IOutreachDraftService _drafts;

    public DrafterStep(IOutreachDraftService drafts)
    {
        _drafts = drafts;
    }

    public async Task RunAsync(RelayState state, CancellationToken cancellationToken)
    {
        var log = state.Log;
        var strong = state.Leads.Where(l => l.Score > state.Options.DraftThreshold).ToList();

        if (strong.Count == 0)
        {
            await log.ResultAsync(Agent, $"No lead scored above {state.Options.DraftThreshold}, so I have nothing to draft.");
            return;
        }

        await log.ThoughtAsync(Agent,
            $"Drafting for {strong.Count} lead(s) scoring above {state.Options.DraftThreshold}. Drafts use only facts from the posting. Rule-based templates, no AI writing.");

        foreach (var lead in strong)
        {
            var opportunity = lead.Opportunity;
            var request = new DraftGenerationRequest(
                TemplateId: null,
                ContactName: "Hiring team",
                CompanyName: opportunity.Company,
                Role: null,
                OpportunityTitle: opportunity.Title,
                SourceEvidenceSnippet: KeywordText.Short(opportunity.EvidenceSnippet ?? string.Empty, 200),
                ServiceProposition: null,
                SenderName: null,
                SenderTitle: null,
                SenderCompany: null,
                JurisdictionPolicy: null);

            var draft = await _drafts.GenerateDraftAsync(request);

            if (!draft.IsValid)
            {
                var problems = string.Join(", ", draft.MissingVariables.Concat(draft.ProhibitedClaims));
                await log.WarningAsync(Agent,
                    $"Draft for {opportunity.Company} did not pass my own check ({(problems.Length == 0 ? "validation failed" : problems)}). Leaving it out.",
                    lead.ToDto(includeScore: true));
                continue;
            }

            lead.Draft = new AgentDraftDto(draft.Subject, $"{draft.Body}\n\n{draft.JurisdictionFooter}");
            await log.ResultAsync(Agent,
                $"Drafted '{draft.Subject}' for {opportunity.Company}. Not sent. It waits for a person.",
                lead.ToDto(includeScore: true, includeDraft: true));
        }

        var drafted = strong.Count(l => l.Draft != null);
        await log.HandoffAsync(Agent, $"Handing {drafted} draft(s) to {AgentCatalog.NameOf(AgentIds.Compliance)} for a compliance check.");
    }
}
