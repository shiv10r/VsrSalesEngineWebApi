namespace VsrSalesEngineWebApi.Core.Entities;

/// <summary>A local business (not a job posting) that VSR could sell software or a website to.</summary>
public class LocalLead
{
    public string Id { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public string CategoryReason { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string NormalizedPhone { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public bool HasWebsite { get; set; }
    public double? Rating { get; set; }
    public int? ReviewCount { get; set; }
    public string Source { get; set; } = "Manual"; // CSV import, Manual
    public string SourceLabel { get; set; } = "Manual";
    public string Status { get; set; } = "New"; // New, Contacted, Interested, Won, Lost
    public string Notes { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = "Sales Member";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
