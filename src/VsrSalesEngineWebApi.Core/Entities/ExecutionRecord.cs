namespace VsrSalesEngineWebApi.Core.Entities;

public class ExecutionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string JobType { get; set; } = "Discovery"; // Discovery, OutreachSend, SequenceStep, Sync
    public string ReferenceId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Running, FailedTransient, FailedPermanent, DeadLetter, Succeeded, Cancelled
    public string FailureClassification { get; set; } = "None"; // None, Transient, Permanent, Timeout, RateLimitExceeded
    public int AttemptCount { get; set; } = 1;
    public int MaxAttempts { get; set; } = 3;
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public int TimeBudgetSeconds { get; set; } = 60;
    public long DurationMs { get; set; } = 0;
    public bool CanRetry => (Status == "FailedTransient" || FailureClassification == "Transient") && AttemptCount < MaxAttempts;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}
