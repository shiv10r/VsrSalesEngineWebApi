using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class ExecutionRepository : IExecutionRepository
{
    private static readonly ConcurrentDictionary<string, ExecutionRecord> _records = new();
    private static readonly ConcurrentDictionary<string, int> _sourceFailures = new();

    static ExecutionRepository()
    {
        // Seed historical execution records
        var rec1 = new ExecutionRecord
        {
            Id = "EXEC-001",
            JobType = "Discovery",
            ReferenceId = "SCHED-001",
            Status = "Succeeded",
            FailureClassification = "None",
            AttemptCount = 1,
            MaxAttempts = 3,
            DurationMs = 1420,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-4),
            CompletedAtUtc = DateTime.UtcNow.AddHours(-4).AddSeconds(1.42)
        };

        var rec2 = new ExecutionRecord
        {
            Id = "EXEC-002",
            JobType = "Discovery",
            ReferenceId = "SRC-001",
            Status = "FailedTransient",
            FailureClassification = "Transient",
            AttemptCount = 1,
            MaxAttempts = 3,
            ErrorMessage = "HTTP 429 Too Many Requests: Upstream rate limit reached on company careers portal",
            NextRetryAtUtc = DateTime.UtcNow.AddMinutes(5),
            DurationMs = 650,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            CompletedAtUtc = DateTime.UtcNow.AddHours(-1).AddSeconds(0.65)
        };

        var rec3 = new ExecutionRecord
        {
            Id = "EXEC-003",
            JobType = "OutreachSend",
            ReferenceId = "CMP-001",
            Status = "DeadLetter",
            FailureClassification = "Permanent",
            AttemptCount = 1,
            MaxAttempts = 1,
            ErrorMessage = "Suppression rule triggered: Domain @blocked-client.example is on the exclusion list",
            DurationMs = 120,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
            CompletedAtUtc = DateTime.UtcNow.AddHours(-2).AddSeconds(0.12)
        };

        _records[rec1.Id] = rec1;
        _records[rec2.Id] = rec2;
        _records[rec3.Id] = rec3;

        _sourceFailures["Company careers"] = 1;
    }

    public Task<ExecutionRecord?> GetByIdAsync(string id)
    {
        _records.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<ExecutionRecord>> GetAllAsync(string? jobType = null, int limit = 50)
    {
        var query = _records.Values.AsEnumerable();
        if (!string.IsNullOrEmpty(jobType))
        {
            query = query.Where(r => r.JobType.Equals(jobType, StringComparison.OrdinalIgnoreCase));
        }

        var list = query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<ExecutionRecord>>(list);
    }

    public Task<IReadOnlyList<ExecutionRecord>> GetFailuresAsync(int limit = 50)
    {
        var list = _records.Values
            .Where(r => r.Status.StartsWith("Failed") || r.Status == "DeadLetter")
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<ExecutionRecord>>(list);
    }

    public Task<IReadOnlyList<ExecutionRecord>> GetDeadLettersAsync(int limit = 50)
    {
        var list = _records.Values
            .Where(r => r.Status == "DeadLetter")
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<ExecutionRecord>>(list);
    }

    public Task AddAsync(ExecutionRecord record)
    {
        _records[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ExecutionRecord record)
    {
        _records[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, int>> GetSourceFailureCountsAsync()
    {
        return Task.FromResult(new Dictionary<string, int>(_sourceFailures));
    }

    public Task RecordSourceFailureAsync(string sourceName)
    {
        _sourceFailures.AddOrUpdate(sourceName, 1, (_, count) => count + 1);
        return Task.CompletedTask;
    }
}
