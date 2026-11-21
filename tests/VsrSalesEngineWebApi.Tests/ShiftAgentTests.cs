using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using VsrSalesEngineWebApi.Controllers;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Data;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using VsrSalesEngineWebApi.Infrastructure.Services;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class ShiftAgentTests
{
    private readonly VsrDbContext _context = TestDbContextFactory.CreateSeeded();
    private readonly AutomationScheduleRepository _repo;
    private readonly DueScheduleRunner _runner;

    public ShiftAgentTests()
    {
        _repo = new AutomationScheduleRepository(_context);
        var service = new ScheduleExecutionService(_repo, new DistributedLockService(_context));
        _runner = new DueScheduleRunner(_repo, service);
    }

    private async Task MakeDueAsync(string id, string quietStart, string quietEnd)
    {
        var schedule = (await _repo.GetByIdAsync(id))!;
        schedule.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-5);
        schedule.QuietHoursStart = quietStart;
        schedule.QuietHoursEnd = quietEnd;
        await _repo.UpdateAsync(schedule);
    }

    [Fact]
    public async Task Seed_CreatesThreeTwoHourShiftAgentsOnLiveSourcesOnly()
    {
        var shifts = (await _runner.GetStatusAsync()).Where(s => s.IntervalHours == 2).ToList();

        Assert.Equal(3, shifts.Count);
        Assert.All(shifts, s =>
        {
            Assert.DoesNotContain("Company careers", s.Sources);
            Assert.DoesNotContain("Approved feed", s.Sources);
        });
    }

    [Fact]
    public async Task RunDue_RunsDueScheduleAndSchedulesNextRunTwoHoursAhead()
    {
        await MakeDueAsync("SCHED-001", "00:00", "00:00"); // no quiet hours

        var summary = await _runner.RunDueAsync("ExternalTimer");

        Assert.Equal(1, summary.SchedulesRun);
        var updated = (await _repo.GetByIdAsync("SCHED-001"))!;
        Assert.Equal("Success", updated.LastRunStatus);
        Assert.InRange(updated.NextRunAtUtc, DateTime.UtcNow.AddMinutes(115), DateTime.UtcNow.AddMinutes(125));
    }

    [Fact]
    public async Task RunDue_SkipsScheduleOutsideItsShiftWindow()
    {
        await MakeDueAsync("SCHED-001", "00:00", "23:59:59"); // quiet all day

        var summary = await _runner.RunDueAsync("ExternalTimer");

        Assert.Equal(0, summary.SchedulesRun);
        Assert.Equal(1, summary.SchedulesSkipped);
        Assert.Equal("SkippedQuietHours", (await _repo.GetByIdAsync("SCHED-001"))!.LastRunStatus);
    }

    [Fact]
    public async Task RunDue_IgnoresSchedulesNotYetDue()
    {
        var summary = await _runner.RunDueAsync("ExternalTimer");

        Assert.Equal(0, summary.SchedulesRun + summary.SchedulesSkipped);
    }

    private AgentsController BuildController(string? configuredKey, string? suppliedKey)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agents:RunKey"] = configuredKey })
            .Build();
        var controller = new AgentsController(_runner, config)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        if (suppliedKey != null) controller.Request.Headers["X-Agent-Key"] = suppliedKey;
        return controller;
    }

    [Fact]
    public async Task RunDueEndpoint_IsDisabledWhenNoKeyConfigured()
    {
        var result = await BuildController(null, "anything").RunDue(CancellationToken.None);

        Assert.Equal(503, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task RunDueEndpoint_RejectsMissingOrWrongKey(string? supplied)
    {
        var result = await BuildController("secret-key", supplied).RunDue(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task RunDueEndpoint_AcceptsCorrectKey()
    {
        var result = await BuildController("secret-key", "secret-key").RunDue(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<DueRunSummaryDto>(ok.Value);
    }
}
