namespace VsrSalesEngineWebApi.Core.Entities;

public class Opportunity
{
    public string Id { get; set; } = string.Empty; // e.g. OP-1048
    public string Company { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string FoundAt { get; set; } = string.Empty;
    public string Status { get; set; } = "New"; // New, Needs review, Qualified, Contacted, Readiness hold, Interested
    public string Fit { get; set; } = "High"; // High, Medium, Low, Unrated
    public string Type { get; set; } = "Individual developer"; // Individual developer, Company/vendor, Direct project
    public bool HasVendorDocRequirements { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string EvidenceSnippet { get; set; } = string.Empty;
    public string MatchedQuery { get; set; } = string.Empty;
    public string DeduplicationKey { get; set; } = string.Empty;
    public DateTime RetrievedAtUtc { get; set; } = DateTime.UtcNow;
}

