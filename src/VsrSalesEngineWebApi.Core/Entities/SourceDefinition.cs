namespace VsrSalesEngineWebApi.Core.Entities;

public class SourceDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Careers, JobBoard, Research, Procurement, Feed
    public string PolicyStatus { get; set; } = "Active"; // Active, Restricted, UnderReview, Disabled
    public bool AllowsDirectFetch { get; set; } = true;
    public string TermsUrl { get; set; } = string.Empty;
    public string AttributionNotice { get; set; } = string.Empty;
    public int DailyRequestLimit { get; set; } = 500;
    public int CurrentDayRequests { get; set; } = 0;
    public string HealthStatus { get; set; } = "Healthy"; // Healthy, Degraded, Down
    public DateTime LastCheckedAtUtc { get; set; } = DateTime.UtcNow;
}
