namespace VsrSalesEngineWebApi.Core.Entities;

public class SuppressionRecord
{
    public string Id { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty; // Email address or domain (e.g. example.com)
    public string Type { get; set; } = "Email"; // Email, Domain
    public string Reason { get; set; } = "OptOut"; // OptOut, Complaint, ManualSuppression, Bounced, BlockedByPolicy
    public string Source { get; set; } = "UnsubscribeLink"; // UnsubscribeLink, UserRequest, AdminAction, SystemDetection
    public string CreatedBy { get; set; } = "System";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
