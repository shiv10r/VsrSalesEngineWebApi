namespace VsrSalesEngineWebApi.Core.Entities;

/// <summary>
/// The owner's agent settings (single row, Id = 1): display names, on/off and the draft threshold.
/// Keywords live on the search profile and shift timing lives on the schedules.
/// </summary>
public class AgentSettings
{
    public int Id { get; set; } = 1;
    public int DraftThreshold { get; set; } = 50;

    /// <summary>JSON list of { id, name, enabled }. Agents missing from the list use their defaults.</summary>
    public string AgentsJson { get; set; } = "[]";

    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
