namespace VsrSalesEngineWebApi.Core.Services.AgentRelay;

public class AgentRelayOptions
{
    /// <summary>
    /// UI pacing only: a short pause after each logged event so a live dashboard is readable.
    /// It does not change what the agents do. 0 disables it (used in tests).
    /// </summary>
    public int StepPauseMs { get; set; }

    /// <summary>Leads scoring above this get a draft.</summary>
    public int DraftThreshold { get; set; } = 50;
}
