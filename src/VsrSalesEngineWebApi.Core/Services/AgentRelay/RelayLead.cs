using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

/// <summary>A posting travelling through the relay.</summary>
public class RelayLead
{
    public RelayLead(Opportunity opportunity)
    {
        Opportunity = opportunity;
    }

    public Opportunity Opportunity { get; }
    public int Score { get; set; }
    public List<string> Reasons { get; set; } = new();
    public string? Email { get; set; }

    public AgentLeadDto ToDto(bool includeScore = false) => new(
        Opportunity.Id,
        Opportunity.Company,
        Opportunity.Title,
        Opportunity.Source,
        includeScore ? Score : null,
        includeScore ? Reasons : Array.Empty<string>(),
        string.IsNullOrWhiteSpace(Opportunity.SourceUrl) ? null : Opportunity.SourceUrl);
}
