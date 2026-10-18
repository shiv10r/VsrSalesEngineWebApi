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
    string SourceUrl,
    string EvidenceSnippet
);

public record CreateOpportunityDto(
    string Company,
    string Title,
    string Source,
    string Location,
    string Fit,
    string Type,
    bool HasVendorDocRequirements,
    string? SourceUrl,
    string? EvidenceSnippet
);

public record OpportunityFilterDto(
    string? SearchQuery,
    string? Status,
    string? Source
);

