namespace VsrSalesEngineWebApi.Core.Entities;

/// <summary>
/// The owner's agent settings (single row, Id = 1): display names and on/off.
/// Keywords live on the search profile and shift timing lives on the schedules.
/// </summary>
public class AgentSettings
{
    public int Id { get; set; } = 1;

    /// <summary>JSON list of { id, name, enabled }. Agents missing from the list use their defaults.</summary>
    public string AgentsJson { get; set; } = "[]";

    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
