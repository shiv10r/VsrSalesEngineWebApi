namespace VsrSalesEngineWebApi.Core.DTOs;

public record OutreachTemplateDto(
    string Id,
    string Name,
    string Category,
    string SubjectTemplate,
    string BodyTemplate,
    string ServiceProposition,
    string JurisdictionPolicy,
    string FooterTemplate,
    bool IsActive,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record CreateOutreachTemplateDto(
    string Name,
    string Category,
    string SubjectTemplate,
    string BodyTemplate,
    string? ServiceProposition,
    string? JurisdictionPolicy,
    string? FooterTemplate
);

public record UpdateOutreachTemplateDto(
    string Name,
    string Category,
    string SubjectTemplate,
    string BodyTemplate,
    string? ServiceProposition,
    string? JurisdictionPolicy,
    string? FooterTemplate,
    bool? IsActive
);

public record DraftGenerationRequest(
    string? TemplateId,
    string ContactName,
    string CompanyName,
    string? Role,
    string? OpportunityTitle,
    string? SourceEvidenceSnippet,
    string? ServiceProposition,
    string? SenderName,
    string? SenderTitle,
    string? SenderCompany,
    string? JurisdictionPolicy
);

public record DraftContentValidationRequest(
    string Subject,
    string Body,
    string? JurisdictionPolicy
);

public record DraftGenerationResult(
    string Subject,
    string Body,
    IReadOnlyList<string> FactsUsed,
    bool IsValid,
    IReadOnlyList<string> MissingVariables,
    IReadOnlyList<string> ProhibitedClaims,
    IReadOnlyList<string> Warnings,
    string Tone,
    string JurisdictionFooter
);

public record DraftValidationResult(
    bool IsValid,
    IReadOnlyList<string> MissingVariables,
    IReadOnlyList<string> ProhibitedClaims,
    IReadOnlyList<string> Warnings,
    bool HasUnsubscribeMechanism,
    bool HasPhysicalAddress,
    bool HasSenderIdentity
);
