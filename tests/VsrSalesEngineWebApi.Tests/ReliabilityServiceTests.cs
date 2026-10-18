using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class ReliabilityServiceTests
{
    private readonly IExecutionRepository _executionRepo;
    private readonly IAutomationScheduleRepository _scheduleRepo;
    private readonly IReliabilityService _reliabilityService;

    public ReliabilityServiceTests()
    {
        _executionRepo = new ExecutionRepository();
        _scheduleRepo = new AutomationScheduleRepository();
        _reliabilityService = new ReliabilityService(_executionRepo, _scheduleRepo);
    }

    [Theory]
    [InlineData("HTTP 429 Too Many Requests: Rate limit exceeded", "RateLimitExceeded")]
    [InlineData("503 Service Unavailable: Gateway hiccup", "Transient")]
    [InlineData("502 Bad Gateway: Connection reset by peer", "Transient")]
    [InlineData("Operation timed out after 30000ms", "Timeout")]
    [InlineData("401 Unauthorized: Invalid API key provided", "Permanent")]
    [InlineData("403 Forbidden: Source domain access disallowed", "Permanent")]
    [InlineData("Prohibited claim detected: Guaranteed results violate policy", "Permanent")]
    public void ClassifyFailure_CategorizesAccurately(string errorMessage, string expectedClassification)
    {
        var result = _reliabilityService.ClassifyFailure(errorMessage);
        Assert.Equal(expectedClassification, result);
    }

    [Fact]
    public void CalculateBackoff_GrowsExponentiallyAndCaps()
    {
        var backoff1 = _reliabilityService.CalculateBackoff(1);
        var backoff2 = _reliabilityService.CalculateBackoff(2);
        var backoff3 = _reliabilityService.CalculateBackoff(3);
        var backoff10 = _reliabilityService.CalculateBackoff(10);

        Assert.Equal(2, backoff1.TotalSeconds);
        Assert.Equal(4, backoff2.TotalSeconds);
        Assert.Equal(8, backoff3.TotalSeconds);
        Assert.Equal(60, backoff10.TotalSeconds); // Capped at 60s
    }

    [Fact]
    public async Task RetryAsync_AllowsEligibleTransientFailure()
    {
        var record = new ExecutionRecord
        {
            Id = $"TEST-EXEC-{Guid.NewGuid():N}",
            JobType = "Discovery",
            Status = "FailedTransient",
            FailureClassification = "Transient",
            AttemptCount = 1,
            MaxAttempts = 3,
            ErrorMessage = "503 Gateway timeout"
        };
        await _executionRepo.AddAsync(record);

        var result = await _reliabilityService.RetryAsync(record.Id);

        Assert.True(result.Success);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(2, result.AttemptCount);
        Assert.NotNull(result.NextRetryAtUtc);
    }

    [Fact]
    public async Task RetryAsync_TransitionsToDeadLetterWhenMaxAttemptsReached()
    {
        var record = new ExecutionRecord
        {
            Id = $"TEST-EXEC-{Guid.NewGuid():N}",
            JobType = "Discovery",
            Status = "FailedTransient",
            FailureClassification = "Transient",
            AttemptCount = 2,
            MaxAttempts = 3,
            ErrorMessage = "503 Gateway timeout"
        };
        await _executionRepo.AddAsync(record);

        var result = await _reliabilityService.RetryAsync(record.Id);

        Assert.True(result.Success);
        Assert.Equal("DeadLetter", result.Status);
        Assert.Equal(3, result.AttemptCount);
    }

    [Fact]
    public async Task RetryAsync_RejectsPermanentFailures()
    {
        var record = new ExecutionRecord
        {
            Id = $"TEST-EXEC-{Guid.NewGuid():N}",
            JobType = "OutreachSend",
            Status = "DeadLetter",
            FailureClassification = "Permanent",
            AttemptCount = 1,
            MaxAttempts = 1,
            ErrorMessage = "Suppression rule matched"
        };
        await _executionRepo.AddAsync(record);

        var result = await _reliabilityService.RetryAsync(record.Id);

        Assert.False(result.Success);
        Assert.Equal("Ineligible", result.Status);
        Assert.Contains("ineligible", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteWithProtectionAsync_SafelyTracksSuccessfulExecution()
    {
        var executed = false;
        var result = await _reliabilityService.ExecuteWithProtectionAsync(
            "Discovery",
            "TEST-REF",
            async (ct) =>
            {
                await Task.Delay(50, ct);
                executed = true;
            },
            timeBudgetSeconds: 5
        );

        Assert.True(executed);
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal("None", result.FailureClassification);
        Assert.True(result.DurationMs >= 40);
    }

    [Fact]
    public async Task GetMetricsAsync_ReturnsOperationalHealthAndLag()
    {
        var metrics = await _reliabilityService.GetMetricsAsync();

        Assert.NotNull(metrics);
        Assert.NotNull(metrics.RepeatedSourceFailures);
        Assert.NotNull(metrics.ActiveAlerts);
        Assert.Contains(metrics.SystemHealth, new[] { "Healthy", "Degraded", "Unhealthy" });
    }
}
