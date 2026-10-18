namespace VsrSalesEngineWebApi.Core.Entities;

public class OutreachTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // Cloud & Architecture, Team Augmentation, Procurement, General
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string ServiceProposition { get; set; } = string.Empty;
    public string JurisdictionPolicy { get; set; } = "US-CANSPAM"; // US-CANSPAM, EU-GDPR, UK-PECR
    public string FooterTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = "System";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
