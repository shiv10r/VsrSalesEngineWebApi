namespace VsrSalesEngineWebApi.Core.DTOs;

public record OutreachSendRequest(
    string RecipientEmail,
    string RecipientName,
    string Subject,
    string Body,
    string? IdempotencyKey,
    bool IsTestMode = true,
    string? SinkAddressOverride = null
);

public record OutreachSendResult(
    bool Success,
    string MessageId,
    string Status,
    string Detail,
    bool Suppressed,
    bool WasRoutedToSink,
    string? ProviderMessageId
);

public record SuppressionDto(
    string Id,
    string Target,
    string Type,
    string Reason,
    string Source,
    string CreatedBy,
    DateTime CreatedAtUtc
);

public record CreateSuppressionDto(
    string Target,
    string? Type,
    string? Reason,
    string? Source
);
