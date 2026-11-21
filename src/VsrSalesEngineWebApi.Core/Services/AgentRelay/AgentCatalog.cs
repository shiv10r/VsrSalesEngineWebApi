namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

public static class AgentIds
{
    public const string Scout = "scout";
    public const string Filter = "filter";
    public const string Dedupe = "dedupe";
    public const string Scorer = "scorer";
    public const string ContactFinder = "contact-finder";
    public const string Drafter = "drafter";
    public const string Compliance = "compliance";
    public const string Reporter = "reporter";
}

public static class EventKinds
{
    public const string Thought = "thought";
    public const string Action = "action";
    public const string Result = "result";
    public const string Handoff = "handoff";
    public const string Warning = "warning";
}

public record AgentInfo(string Id, string Name, string Role);

/// <summary>The relay order. Index is used to tell which agents are still waiting during a run.</summary>
public static class AgentCatalog
{
    public static readonly IReadOnlyList<AgentInfo> All = new[]
    {
        new AgentInfo(AgentIds.Scout, "Scout", "Reads the live job feeds for fresh contract postings"),
        new AgentInfo(AgentIds.Filter, "Filter", "Drops postings that are not software work or hit your exclusions"),
        new AgentInfo(AgentIds.Dedupe, "Dedupe", "Skips postings you already have"),
        new AgentInfo(AgentIds.Scorer, "Scorer", "Scores each new lead with transparent rules"),
        new AgentInfo(AgentIds.ContactFinder, "Contact Finder", "Looks for a public contact email inside the posting"),
        new AgentInfo(AgentIds.Drafter, "Drafter", "Writes a grounded outreach draft for strong leads (nothing is sent)"),
        new AgentInfo(AgentIds.Compliance, "Compliance", "Checks the suppression list and opt-out footer on every draft"),
        new AgentInfo(AgentIds.Reporter, "Reporter", "Writes the run summary for the dashboard")
    };

    public static int IndexOf(string agentId)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i].Id == agentId) return i;
        }
        return -1;
    }
}
