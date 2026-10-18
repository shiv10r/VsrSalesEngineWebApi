namespace VsrSalesEngineWebApi.Core.DTOs;

public record OpportunityDto(
    string Id,
    string Company,
    string Title,
    string Source,
    string Location,
    string FoundAt,
    string Status,
    string Fit,
    string Type,
    bool HasVendorDocRequirements,
    string? VendorRequirementNotes,
    string SourceUrl,
    string EvidenceSnippet,
    DateTime RetrievedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<StatusChangeDto> StatusHistory
);

public record StatusChangeDto(
    string FromStatus,
    string ToStatus,
    string ChangedBy,
    string? Reason,
    DateTime ChangedAtUtc
);

public record CreateOpportunityDto(
    string Company,
    string Title,
    string Source,
    string Location,
    string Fit,
    string Type,
    bool HasVendorDocRequirements,
    string? VendorRequirementNotes,
    string? SourceUrl,
    string? EvidenceSnippet,
    string? MatchedQuery
);

public record UpdateOpportunityDto(
    string? Company,
    string? Title,
    string? Location,
    string? Fit,
    string? Type,
    bool? HasVendorDocRequirements,
    string? VendorRequirementNotes,
    string? EvidenceSnippet
);

public record TransitionStatusDto(
    string NewStatus,
    string? Reason
);

public record OpportunityFilterDto(
    string? SearchQuery,
    string? Status,
    string? Source,
    string? Fit,
    bool? HasVendorDocs,
    int Page = 1,
    int PageSize = 50
);

public record PagedResultDto<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
