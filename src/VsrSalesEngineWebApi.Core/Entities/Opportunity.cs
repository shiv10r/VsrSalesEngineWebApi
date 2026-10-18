namespace VsrSalesEngineWebApi.Core.Entities;

public class Opportunity
{
    public string Id { get; set; } = string.Empty; // e.g. OP-1048
    public string Company { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string FoundAt { get; set; } = string.Empty;
    public string Status { get; set; } = "New"; // New, Needs review, Qualified, Contacted, Readiness hold, Interested, Archived
    public string Fit { get; set; } = "High"; // High, Medium, Low, Unrated
    public string Type { get; set; } = "Individual developer"; // Individual developer, Company/vendor, Direct project
    public bool HasVendorDocRequirements { get; set; }
    public string? VendorRequirementNotes { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string EvidenceSnippet { get; set; } = string.Empty;
    public string MatchedQuery { get; set; } = string.Empty;
    public string DeduplicationKey { get; set; } = string.Empty;
    public DateTime RetrievedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<OpportunityStatusChange> StatusHistory { get; set; } = new();
}

public class OpportunityStatusChange
{
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
