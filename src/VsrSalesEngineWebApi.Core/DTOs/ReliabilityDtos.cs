namespace VsrSalesEngineWebApi.Core.DTOs;

public record ExecutionRecordDto(
    string Id,
    string JobType,
    string ReferenceId,
    string Status,
    string FailureClassification,
    int AttemptCount,
    int MaxAttempts,
    string? ErrorMessage,
    DateTime? NextRetryAtUtc,
    int TimeBudgetSeconds,
    long DurationMs,
    bool CanRetry,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc
);

public record ReliabilityMetricsDto(
    Dictionary<string, int> RepeatedSourceFailures,
    int SchedulerLagSeconds,
    int DeadLetterCount,
    int FailedSendCount,
    long AverageRunDurationMs,
    string SystemHealth,
    List<string> ActiveAlerts
);

public record RetryExecutionResultDto(
    string ExecutionId,
    bool Success,
    string Status,
    string Message,
    DateTime? NextRetryAtUtc,
    int AttemptCount
);

public record SimulateFailureRequestDto(
    string FailureType, // "Transient", "Permanent", "Timeout", "RateLimitExceeded"
    string? JobType,
    string? ReferenceId
);

public record SmokeCheckItemDto(
    string Component,
    string Status,
    string Detail
);

public record OperationalSmokeDrillResultDto(
    string Status,
    bool AllChecksPassed,
    List<SmokeCheckItemDto> Checks,
    DateTime TimestampUtc
);
