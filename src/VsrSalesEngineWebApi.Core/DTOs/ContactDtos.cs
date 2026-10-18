namespace VsrSalesEngineWebApi.Core.DTOs;

public record ContactDto(
    string Id,
    string Name,
    string Company,
    string Role,
    string Email,
    string Source,
    string SourceUrl,
    string ChannelBasis,
    string Status,
    string QualificationStatus,
    bool IsRoleAccount,
    string? Notes,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record CreateContactDto(
    string Name,
    string Company,
    string Role,
    string Email,
    string? Source,
    string? SourceUrl,
    string? ChannelBasis,
    string? Notes
);

public record UpdateContactDto(
    string? Name,
    string? Company,
    string? Role,
    string? Email,
    string? SourceUrl,
    string? Notes
);

public record QualifyContactDto(
    string QualificationStatus,
    string? Status,
    string? Notes
);

public record ContactFilterDto(
    string? SearchQuery,
    string? Company,
    string? Status,
    string? QualificationStatus,
    int Page = 1,
    int PageSize = 50
);
