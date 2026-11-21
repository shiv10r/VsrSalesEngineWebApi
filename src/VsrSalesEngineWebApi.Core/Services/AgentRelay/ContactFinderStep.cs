using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>Step 5: records public contact emails found inside postings. LinkedIn is never automated.</summary>
public class ContactFinderStep : IRelayStep
{
    private const string Agent = AgentIds.ContactFinder;
    public string AgentId => Agent;

    private static readonly HashSet<string> RoleMailboxes = new(StringComparer.OrdinalIgnoreCase)
    {
        "info", "hr", "jobs", "careers", "hello", "contact", "team", "support", "sales", "admin", "recruiting"
    };

    private readonly IContactRepository _contacts;

    public ContactFinderStep(IContactRepository contacts)
    {
        _contacts = contacts;
    }

    public async Task RunAsync(RelayState state, CancellationToken cancellationToken)
    {
        var log = state.Log;
        if (state.Leads.Count == 0)
        {
            await log.ResultAsync(Agent, "No new leads, so no contacts to look for.");
            return;
        }

        await log.ThoughtAsync(Agent, "Reading each new posting for a public contact email. I only use emails the poster published themselves.");

        var withEmail = 0;
        var quietNoEmail = 0;

        foreach (var lead in state.Leads)
        {
            var opportunity = lead.Opportunity;
            var strong = lead.Score > state.DraftThreshold;

            if (lead.Email == null)
            {
                if (strong)
                {
                    await log.WarningAsync(Agent,
                        $"No public email in '{KeywordText.Short(opportunity.Title, 60)}' at {opportunity.Company}. Needs a manual LinkedIn handoff (I never automate LinkedIn).",
                        lead.ToDto(includeScore: true));
                }
                else
                {
                    quietNoEmail++;
                }
                continue;
            }

            withEmail++;
            var created = await EnsureContactAsync(lead, cancellationToken);
            if (strong || created)
            {
                var note = created ? "Added to Contacts" : "Already in Contacts";
                await log.ResultAsync(Agent,
                    $"Found a public email for {opportunity.Company}: {lead.Email}. {note}.",
                    lead.ToDto(includeScore: true));
            }
        }

        await log.ResultAsync(Agent,
            $"{withEmail} of {state.Leads.Count} lead(s) have a public email. {quietNoEmail} lower-scoring lead(s) have none and stay on the list for manual review.");
        await log.HandoffAsync(Agent, state.StrongLeads == 0
            ? "No lead scored high enough for a draft."
            : $"Handing {state.StrongLeads} strong lead(s) to {state.NameOf(AgentIds.Drafter)} for drafts.");
    }

    private async Task<bool> EnsureContactAsync(RelayLead lead, CancellationToken ct)
    {
        var normalized = lead.Email!.Trim().ToLowerInvariant();
        if (await _contacts.GetByNormalizedEmailAsync(normalized, ct) != null) return false;

        var opportunity = lead.Opportunity;
        var localPart = normalized.Split('@')[0];

        await _contacts.AddAsync(new Contact
        {
            Id = $"CON-AG-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Name = "Hiring contact",
            Company = opportunity.Company,
            Role = "Hiring contact",
            Email = lead.Email,
            NormalizedEmail = normalized,
            Source = opportunity.Source,
            SourceUrl = opportunity.SourceUrl,
            ChannelBasis = "ExplicitBusinessContact",
            IsRoleAccount = RoleMailboxes.Contains(localPart),
            Notes = $"Published in the posting '{opportunity.Title}'.",
            CreatedBy = "Agent: Contact Finder"
        }, ct);

        return true;
    }
}
