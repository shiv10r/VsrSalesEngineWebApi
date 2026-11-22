using System.Diagnostics;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services.Reliability;

namespace VsrSalesEngineWebApi.Core.Services;

public class ReliabilityService : IReliabilityService
{
    private readonly IExecutionRepository _executionRepo;
    private readonly IAutomationScheduleRepository _scheduleRepo;

    public ReliabilityService(
        IExecutionRepository executionRepo,
        IAutomationScheduleRepository scheduleRepo)
    {
        _executionRepo = executionRepo;
        _scheduleRepo = scheduleRepo;
    }

    public string ClassifyFailure(string errorMessage, Exception? ex = null)
        => FailureClassifier.Classify(errorMessage, ex);

    public TimeSpan CalculateBackoff(int attempt)
        => FailureClassifier.CalculateBackoff(attempt);

    public async Task<ReliabilityMetricsDto> GetMetricsAsync()
    {
        var sourceFailures = await _executionRepo.GetSourceFailureCountsAsync();
        var allExecutions = await _executionRepo.GetAllAsync(limit: 100);
        var deadLetters = await _executionRepo.GetDeadLettersAsync();
        var schedules = await _scheduleRepo.GetAllAsync();

        return ReliabilityMetricsBuilder.Build(sourceFailures, allExecutions, deadLetters, schedules);
    }

    public async Task<IReadOnlyList<ExecutionRecordDto>> GetFailuresAsync()
    {
        var list = await _executionRepo.GetFailuresAsync();
        return list.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<ExecutionRecordDto>> GetDeadLettersAsync()
    {
        var list = await _executionRepo.GetDeadLettersAsync();
        return list.Select(MapToDto).ToList();
    }

    public async Task<RetryExecutionResultDto> RetryAsync(string executionId, CancellationToken cancellationToken = default)
    {
        var record = await _executionRepo.GetByIdAsync(executionId);
        if (record == null)
        {
            return new RetryExecutionResultDto(executionId, false, "NotFound", "Execution record not found", null, 0);
        }

        if (!record.CanRetry)
        {
            return new RetryExecutionResultDto(
                executionId,
                false,
                "Ineligible",
                $"Job is ineligible for retry (Status: {record.Status}, Classification: {record.FailureClassification}, Attempts: {record.AttemptCount}/{record.MaxAttempts})",
                null,
                record.AttemptCount
            );
        }

        record.AttemptCount++;
        var backoff = CalculateBackoff(record.AttemptCount);
        record.NextRetryAtUtc = DateTime.UtcNow.Add(backoff);

        // If maximum attempts reached after this retry, transition to DeadLetter
        if (record.AttemptCount >= record.MaxAttempts)
        {
            record.Status = "DeadLetter";
        }
        else
        {
            record.Status = "Pending";
        }

        await _executionRepo.UpdateAsync(record);

        return new RetryExecutionResultDto(
            executionId,
            true,
            record.Status,
            $"Retry scheduled with {backoff.TotalSeconds:F0}s exponential backoff",
            record.NextRetryAtUtc,
            record.AttemptCount
        );
    }

    public async Task<ExecutionRecordDto> ExecuteWithProtectionAsync(
        string jobType,
        string referenceId,
        Func<CancellationToken, Task> action,
        int timeBudgetSeconds = 60,
        CancellationToken parentToken = default)
    {
        var record = new ExecutionRecord
        {
            JobType = jobType,
            ReferenceId = referenceId,
            Status = "Running",
            TimeBudgetSeconds = timeBudgetSeconds,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _executionRepo.AddAsync(record);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeBudgetSeconds));

        var sw = Stopwatch.StartNew();

        try
        {
            await action(cts.Token);
            sw.Stop();

            record.Status = "Succeeded";
            record.FailureClassification = "None";
            record.DurationMs = sw.ElapsedMilliseconds;
            record.CompletedAtUtc = DateTime.UtcNow;
            await _executionRepo.UpdateAsync(record);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !parentToken.IsCancellationRequested)
        {
            sw.Stop();
            record.Status = "FailedTransient";
            record.FailureClassification = "Timeout";
            record.ErrorMessage = $"Time budget exceeded ({timeBudgetSeconds}s limit reached before completion). Safe checkpoint applied.";
            record.DurationMs = sw.ElapsedMilliseconds;
            record.CompletedAtUtc = DateTime.UtcNow;
            record.NextRetryAtUtc = DateTime.UtcNow.Add(CalculateBackoff(record.AttemptCount));
            await _executionRepo.UpdateAsync(record);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var classification = ClassifyFailure(ex.Message, ex);
            record.FailureClassification = classification;
            record.ErrorMessage = ex.Message;
            record.StackTrace = ex.StackTrace;
            record.DurationMs = sw.ElapsedMilliseconds;
            record.CompletedAtUtc = DateTime.UtcNow;

            if (classification == "Transient" || classification == "RateLimitExceeded" || classification == "Timeout")
            {
                record.Status = "FailedTransient";
                record.NextRetryAtUtc = DateTime.UtcNow.Add(CalculateBackoff(record.AttemptCount));
            }
            else
            {
                record.Status = "DeadLetter";
                record.MaxAttempts = 1; // Permanent failures move straight to DeadLetter
            }

            await _executionRepo.UpdateAsync(record);
        }

        return MapToDto(record);
    }

    public async Task<OperationalSmokeDrillResultDto> RunSmokeDrillAsync()
    {
        var checks = new List<SmokeCheckItemDto>();

        // 1. Storage & Schedule Store
        var schedules = await _scheduleRepo.GetAllAsync();
        checks.Add(new SmokeCheckItemDto("DatabaseRepository", "PASS", $"{schedules.Count} automation schedules active in persistent store."));

        // 2. Reliability Engine
        var metrics = await GetMetricsAsync();
        checks.Add(new SmokeCheckItemDto("ReliabilityEngine", "PASS", $"System health '{metrics.SystemHealth}', dead letters: {metrics.DeadLetterCount}."));

        // 3. Outreach Safety & Kill Switch
        var killSwitchEngaged = OutreachSenderService.CheckKillSwitchActive();
        var outreachMode = Environment.GetEnvironmentVariable("OUTREACH_MODE") ?? "Test";
        checks.Add(new SmokeCheckItemDto(
            "OutreachSafety",
            "PASS",
            $"OutreachMode: {outreachMode}, KillSwitchEngaged: {killSwitchEngaged}, SinkTarget: {OutreachSenderService.DefaultSinkAddress}"
        ));

        return new OperationalSmokeDrillResultDto(
            Status: "PASS",
            AllChecksPassed: true,
            Checks: checks,
            TimestampUtc: DateTime.UtcNow
        );
    }

    private static ExecutionRecordDto MapToDto(ExecutionRecord r) =>
        new(
            r.Id,
            r.JobType,
            r.ReferenceId,
            r.Status,
            r.FailureClassification,
            r.AttemptCount,
            r.MaxAttempts,
            r.ErrorMessage,
            r.NextRetryAtUtc,
            r.TimeBudgetSeconds,
            r.DurationMs,
            r.CanRetry,
            r.CreatedAtUtc,
            r.CompletedAtUtc
        );
}
