using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

public interface IRelayStep
{
    string AgentId { get; }
    Task RunAsync(RelayState state, CancellationToken cancellationToken);
}

/// <summary>Everything the eight steps share during one run.</summary>
public class RelayState
{
    public RelayState(AgentRun run, RelayRequest request, AgentLog log, AgentRelayOptions options)
    {
        Run = run;
        Request = request;
        Log = log;
        Options = options;
    }

    public AgentRun Run { get; }
    public RelayRequest Request { get; }
    public AgentLog Log { get; }
    public AgentRelayOptions Options { get; }

    /// <summary>The owner's settings for this run: current agent names, which agents are off, draft threshold.</summary>
    public AgentRuntimeSettings Runtime { get; set; } = AgentRuntimeSettings.Defaults(50);

    public int DraftThreshold => Runtime.DraftThreshold;
    public string NameOf(string agentId) => Runtime.NameOf(agentId);
    public bool IsEnabled(string agentId) => Runtime.IsEnabled(agentId);

    public SearchProfile? Profile { get; set; }
    public IReadOnlyList<string> TargetKeywords { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ExcludedKeywords { get; set; } = Array.Empty<string>();

    public List<RelayLead> Leads { get; } = new();
    public int PostingsRead { get; set; }
    public int SourcesReached { get; set; }
    public int SourcesFailed { get; set; }

    /// <summary>Sources not read on purpose: switched off by policy, or no adapter exists. Not a failure.</summary>
    public int SourcesSkipped { get; set; }
    public int Rejected { get; set; }
    public int Duplicates { get; set; }
    public int NewLeads { get; set; }
    public int StrongLeads { get; set; }
    public int DraftsReady { get; set; }
}
