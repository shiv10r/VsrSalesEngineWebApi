namespace VsrSalesEngineWebApi.Core.DTOs;

public record LocalLeadDto(
    string Id,
    string BusinessName,
    string Category,
    string CategoryReason,
    string District,
    string Address,
    string Phone,
    string Website,
    bool HasWebsite,
    double? Rating,
    int? ReviewCount,
    string Source,
    string SourceLabel,
    string Status,
    string Notes,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record LocalLeadOptionsDto(
    IReadOnlyList<string> Districts,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Statuses
);

public record LocalLeadFilterDto(
    string? District,
    string? Category,
    string? Status,
    bool? HasWebsite,
    string? Q,
    int Page = 1,
    int PageSize = 25
);

public record LocalLeadPageDto(
    IReadOnlyList<LocalLeadDto> Items,
    int Total,
    int Page,
    int PageSize
);

public record DistrictCountDto(string District, int Count);
public record CategoryCountDto(string Category, int Count);
public record GridCellDto(string District, string Category, int Count);

public record LocalLeadSummaryDto(
    int Total,
    int NoWebsite,
    IReadOnlyList<DistrictCountDto> ByDistrict,
    IReadOnlyList<CategoryCountDto> ByCategory,
    IReadOnlyList<GridCellDto> Grid
);

/// <summary>Minimal projection used to build the summary without loading whole rows.</summary>
public record LocalLeadSummaryRow(string District, string Category, bool HasWebsite);

public record CreateLocalLeadDto(
    string BusinessName,
    string District,
    string? Category = null,
    string? Address = null,
    string? Phone = null,
    string? Website = null,
    double? Rating = null,
    int? ReviewCount = null,
    string? Notes = null
);

public record UpdateLocalLeadDto(
    string? Status = null,
    string? Notes = null,
    string? Category = null,
    string? District = null,
    string? Phone = null,
    string? Website = null
);

public record ImportLocalLeadRowDto(
    string? BusinessName,
    string? District = null,
    string? Category = null,
    string? Address = null,
    string? Phone = null,
    string? Website = null,
    double? Rating = null,
    int? ReviewCount = null
);

public record ImportLocalLeadsRequestDto(
    string? SourceLabel,
    List<ImportLocalLeadRowDto>? Rows
);

public record ImportRowErrorDto(int Row, string Message);

public record ImportLocalLeadsResultDto(
    int Imported,
    int Duplicates,
    int Skipped,
    IReadOnlyList<ImportRowErrorDto> Errors
);
