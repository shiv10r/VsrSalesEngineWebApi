namespace VsrSalesEngineWebApi.Core.Entities;

/// <summary>One pass of the agent relay (a shift run, a timer run or a manual run).</summary>
public class AgentRun
{
    public string Id { get; set; } = string.Empty;
    public string ScheduleId { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public string Trigger { get; set; } = "Manual"; // ExternalTimer, AutonomousWorker, Manual
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAtUtc { get; set; }
    public string Status { get; set; } = "running"; // running, completed, failed, skipped
    public string? Summary { get; set; }
    public int NewLeads { get; set; }
    public int Duplicates { get; set; }
    public int Rejected { get; set; }
}

/// <summary>One line in the agents' activity log. Ids increase monotonically so clients can poll with afterId.</summary>
public class AgentEvent
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string AgentId { get; set; } = string.Empty;
    public string Kind { get; set; } = "action"; // thought, action, result, handoff, warning
    public string Message { get; set; } = string.Empty;
    public string? LeadJson { get; set; } // serialized AgentLeadDto when the event is about one lead
}
