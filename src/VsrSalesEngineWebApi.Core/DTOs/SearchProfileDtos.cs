namespace VsrSalesEngineWebApi.Core.DTOs;

public record SearchProfileDto(
    string Id,
    string Name,
    string TargetKeywords,
    string ExcludedKeywords,
    string Location,
    string OpportunityType,
    int PublishedWithinDays,
    IReadOnlyList<string> SelectedSources,
    int MaxResults,
    bool IsActive,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record CreateSearchProfileDto(
    string Name,
    string TargetKeywords,
    string? ExcludedKeywords,
    string Location,
    string? OpportunityType,
    int? PublishedWithinDays,
    IReadOnlyList<string> SelectedSources,
    int? MaxResults
);

public record UpdateSearchProfileDto(
    string? Name,
    string? TargetKeywords,
    string? ExcludedKeywords,
    string? Location,
    string? OpportunityType,
    int? PublishedWithinDays,
    IReadOnlyList<string>? SelectedSources,
    int? MaxResults,
    bool? IsActive
);

public record RunDiscoveryRequestDto(
    string? ProfileId,
    string? TargetKeywords,
    string? ExcludedKeywords,
    string? Location,
    IReadOnlyList<string>? SelectedSources,
    int? MaxResults
);

public record DiscoveryRunResultDto(
    string RunId,
    string InitiatedBy,
    string ProfileName,
    DateTime InitiatedAtUtc,
    DateTime CompletedAtUtc,
    string Status,
    int EvaluatedCount,
    int NewMatchesCount,
    int DuplicateCount,
    IReadOnlyList<SourceMetricDto> SourceMetrics
);

public record SourceMetricDto(
    string SourceName,
    int RecordsFetched,
    int NewMatches,
    int RejectedByPolicy,
    string Status
);
