namespace VsrSalesEngineWebApi.Core.Entities;

public class Contact
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string Source { get; set; } = "Manual entry";
    public string SourceUrl { get; set; } = string.Empty;
    public string ChannelBasis { get; set; } = "ExplicitBusinessContact"; // ExplicitBusinessContact, PublicDirectory, ManualHandoff
    public string Status { get; set; } = "Not contacted"; // Not contacted, Replied, Follow-up due, Do not contact, Qualified
    public string QualificationStatus { get; set; } = "Unverified"; // Unverified, Verified, BlockedByPolicy, OptedOut
    public bool IsRoleAccount { get; set; } = false;
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = "Sales Member";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
