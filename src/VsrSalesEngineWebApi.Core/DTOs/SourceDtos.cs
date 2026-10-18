namespace VsrSalesEngineWebApi.Core.DTOs;

public record SourceDefinitionDto(
    string Id,
    string Name,
    string Description,
    string Category,
    string PolicyStatus,
    bool AllowsDirectFetch,
    string TermsUrl,
    string AttributionNotice,
    int DailyRequestLimit,
    int CurrentDayRequests,
    string HealthStatus,
    DateTime LastCheckedAtUtc
);

public record UpdateSourceStatusDto(
    string PolicyStatus,
    string? Reason
);
