namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

public class AgentRelayOptions
{
    /// <summary>
    /// UI pacing only: a short pause after each logged event so a live dashboard is readable.
    /// It does not change what the agents do. 0 disables it (used in tests).
    /// </summary>
    public int StepPauseMs { get; set; }

    /// <summary>At most this many company websites are read per run (the strongest leads first).</summary>
    public int MaxSiteLookups { get; set; } = 10;
}
