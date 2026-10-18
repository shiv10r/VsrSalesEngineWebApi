namespace VsrSalesEngineWebApi.Core.Entities;

public class SearchProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TargetKeywords { get; set; } = string.Empty;
    public string ExcludedKeywords { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string OpportunityType { get; set; } = "All relevant types";
    public int PublishedWithinDays { get; set; } = 7;
    public List<string> SelectedSources { get; set; } = new();
    public int MaxResults { get; set; } = 50;
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = "System";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DiscoveryRunResult
{
    public string RunId { get; set; } = string.Empty;
    public string InitiatedBy { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public DateTime InitiatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Completed";
    public int EvaluatedCount { get; set; }
    public int NewMatchesCount { get; set; }
    public int DuplicateCount { get; set; }
    public List<SourceRunMetric> SourceMetrics { get; set; } = new();
}

public class SourceRunMetric
{
    public string SourceName { get; set; } = string.Empty;
    public int RecordsFetched { get; set; }
    public int NewMatches { get; set; }
    public int RejectedByPolicy { get; set; }
    public string Status { get; set; } = "Success";
}
