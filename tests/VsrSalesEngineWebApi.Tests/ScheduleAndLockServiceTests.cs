using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class ScheduleAndLockServiceTests
{
    private readonly AutomationScheduleRepository _scheduleRepo = new();
    private readonly DistributedLockService _lockService = new();
    private readonly ScheduleExecutionService _service;

    public ScheduleAndLockServiceTests()
    {
        _service = new ScheduleExecutionService(_scheduleRepo, _lockService);
    }

    [Fact]
    public async Task GetSchedulesAsync_ReturnsSeededSchedules()
    {
        var schedules = await _service.GetSchedulesAsync();

        Assert.NotEmpty(schedules);
        Assert.Contains(schedules, s => s.Frequency == "Daily");
        Assert.Contains(schedules, s => s.Frequency == "Weekly");
    }

    [Fact]
    public async Task CreateScheduleAsync_CalculatesNextRunPreview()
    {
        var dto = new CreateScheduleDto(
            Name: "Custom Nightly Discovery",
            Description: "Scans corporate careers at 11 PM",
            Frequency: "Daily",
            CronExpression: "0 23 * * *",
            Timezone: "UTC",
            ProfileId: "PROF-001",
            SelectedSources: new List<string> { "Company careers" },
            RunCap: 40,
            QuietHoursStart: "22:00",
            QuietHoursEnd: "07:00",
            IsEnabled: true
        );

        var created = await _service.CreateScheduleAsync(dto, "Scheduler Lead");

        Assert.NotNull(created);
        Assert.StartsWith("SCHED-", created.Id);
        Assert.True(created.NextRunAtUtc > DateTime.UtcNow);
        Assert.Equal("Ready", created.LastRunStatus);
    }

    [Fact]
    public async Task ToggleScheduleAsync_UpdatesEnabledState()
    {
        var updated = await _service.ToggleScheduleAsync("SCHED-001", false);

        Assert.NotNull(updated);
        Assert.False(updated.IsEnabled);

        var reenabled = await _service.ToggleScheduleAsync("SCHED-001", true);
        Assert.NotNull(reenabled);
        Assert.True(reenabled.IsEnabled);
    }

    [Fact]
    public async Task DistributedLockService_BlocksConcurrentAcquisition()
    {
        var lockKey = $"lock:test:{Guid.NewGuid():N}";
        var worker1 = "worker-alpha";
        var worker2 = "worker-beta";

        // Worker 1 acquires lock
        var acquired1 = await _lockService.TryAcquireLockAsync(lockKey, worker1, TimeSpan.FromMinutes(2));
        Assert.True(acquired1);

        // Worker 2 attempts to acquire same lock -> must fail
        var acquired2 = await _lockService.TryAcquireLockAsync(lockKey, worker2, TimeSpan.FromMinutes(2));
        Assert.False(acquired2);

        // Worker 1 releases lock
        await _lockService.ReleaseLockAsync(lockKey, worker1);

        // Worker 2 can now acquire lock
        var acquired2AfterRelease = await _lockService.TryAcquireLockAsync(lockKey, worker2, TimeSpan.FromMinutes(2));
        Assert.True(acquired2AfterRelease);
    }

    [Fact]
    public async Task ExecuteScheduleNowAsync_ExecutesSuccessfullyAndUpdatesNextRun()
    {
        var result = await _service.ExecuteScheduleNowAsync("SCHED-001", "IntegrationTest");

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Completed", result.Status);
        Assert.NotNull(result.NextRunAtUtc);

        var updated = await _service.GetScheduleByIdAsync("SCHED-001");
        Assert.NotNull(updated);
        Assert.Equal("Success", updated.LastRunStatus);
        Assert.NotNull(updated.LastRunAtUtc);
    }

    [Fact]
    public async Task ExecuteScheduleNowAsync_WithActiveLock_SafelyBlocksDoubleProcessing()
    {
        var scheduleId = "SCHED-002";
        var lockKey = $"lock:schedule:{scheduleId}";
        var existingOwner = "pre-existing-worker";

        // Acquire lock manually
        await _lockService.TryAcquireLockAsync(lockKey, existingOwner, TimeSpan.FromMinutes(5));

        // Attempt execution while lock is held
        var result = await _service.ExecuteScheduleNowAsync(scheduleId, "ContentionTest");

        Assert.False(result.Success);
        Assert.Equal("ConcurrencyLocked", result.Status);
        Assert.Contains("lease", result.Message, StringComparison.OrdinalIgnoreCase);

        // Clean up
        await _lockService.ReleaseLockAsync(lockKey, existingOwner);
    }
}
