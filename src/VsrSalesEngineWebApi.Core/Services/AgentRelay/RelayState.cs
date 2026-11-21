using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

public interface IRelayStep
{
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

    public SearchProfile? Profile { get; set; }
    public IReadOnlyList<string> TargetKeywords { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ExcludedKeywords { get; set; } = Array.Empty<string>();

    public List<RelayLead> Leads { get; } = new();
    public int PostingsRead { get; set; }
    public int SourcesReached { get; set; }
    public int SourcesFailed { get; set; }
    public int Rejected { get; set; }
    public int Duplicates { get; set; }
    public int NewLeads { get; set; }
    public int StrongLeads { get; set; }
    public int DraftsReady { get; set; }
}
