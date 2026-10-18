namespace VsrSalesEngineWebApi.Core.DTOs;

public record CampaignDto(
    string Id,
    string Name,
    string Audience,
    string Mode,
    string Status,
    int DailySendLimit,
    int CurrentDaySent,
    int MaxTouches,
    int DelayDaysBetweenTouches,
    bool StopOnReply,
    bool StopOnOptOut,
    bool StopOnBounce,
    string SelectedTemplateId,
    string SenderName,
    string SenderEmail,
    string SenderAddress,
    string Jurisdiction,
    string OptOutMechanismUrl,
    int TotalSent,
    int TotalReplies,
    int TotalOptOuts,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record CreateCampaignDto(
    string Name,
    string Audience,
    string? Mode,
    int? DailySendLimit,
    int? MaxTouches,
    int? DelayDaysBetweenTouches,
    string? SelectedTemplateId,
    string? SenderName,
    string? SenderEmail,
    string? SenderAddress,
    string? Jurisdiction,
    string? OptOutMechanismUrl
);

public record UpdateCampaignDto(
    string Name,
    string Audience,
    string? Mode,
    string? Status,
    int? DailySendLimit,
    int? MaxTouches,
    int? DelayDaysBetweenTouches,
    string? SelectedTemplateId,
    string? SenderName,
    string? SenderEmail,
    string? SenderAddress,
    string? Jurisdiction,
    string? OptOutMechanismUrl
);

public record UpdateCampaignStatusDto(string Status, string? Reason = null);

public record UpdateCampaignModeDto(string Mode);

public record ComplianceGateResultDto(
    bool IsProductionReady,
    bool SenderIdentityConfigured,
    bool ProviderCredentialsConfigured,
    bool PhysicalAddressConfigured,
    bool OptOutMechanismConfigured,
    bool JurisdictionPolicyConfigured,
    bool RateLimitsConfigured,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> RemediationSteps
);

public record ContactSequenceCheckRequest(
    string ContactEmail,
    int CurrentTouches,
    bool HasReplied = false,
    bool HasOptedOut = false,
    bool HasBounced = false
);

public record ContactSequenceCheckResult(
    bool CanSendNextStep,
    int NextStepNumber,
    int RecommendedDelayDays,
    string Status,
    string Reason
);
