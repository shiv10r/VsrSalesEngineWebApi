using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Moq;
using VsrSalesEngineWebApi.Controllers;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

[Collection("OutreachSenderStateCollection")]
public class DeploymentAndOperationalSmokeTests
{
    private readonly Mock<IHostEnvironment> _envMock;
    private readonly IOpportunityRepository _opportunityRepo;
    private readonly ISourceRegistryRepository _sourceRegistry;
    private readonly IExecutionRepository _executionRepo;
    private readonly IReliabilityService _reliabilityService;
    private readonly ISuppressionRepository _suppressionRepo;
    private readonly OutreachSenderService _outreachSender;
    private readonly HealthController _healthController;
    private readonly ReliabilityController _reliabilityController;

    public DeploymentAndOperationalSmokeTests()
    {
        _envMock = new Mock<IHostEnvironment>();
        _envMock.Setup(e => e.EnvironmentName).Returns("Production");

        _opportunityRepo = new OpportunityRepository();
        _sourceRegistry = new SourceRegistryRepository();
        _executionRepo = new ExecutionRepository();
        var scheduleRepo = new AutomationScheduleRepository();
        _reliabilityService = new ReliabilityService(_executionRepo, scheduleRepo);
        _suppressionRepo = new SuppressionRepository();
        _outreachSender = new OutreachSenderService(_suppressionRepo);

        _healthController = new HealthController(_envMock.Object);
        _reliabilityController = new ReliabilityController(
            _reliabilityService,
            _envMock.Object
        );
    }

    [Fact]
    public void RootHealthEndpoint_ReturnsHealthyStatus()
    {
        var actionResult = _healthController.Check();
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.NotNull(okResult.Value);

        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("VsrSalesEngineWebApi", doc.RootElement.GetProperty("service").GetString());
        Assert.Equal("Production", doc.RootElement.GetProperty("environment").GetString());
    }

    [Fact]
    public async Task OperationalSmokeDrillEndpoint_ReturnsPassWithAllComponentsHealthy()
    {
        var actionResult = await _reliabilityController.RunOperationalSmokeDrill();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<OperationalSmokeDrillResultDto>(okResult.Value);

        Assert.Equal("PASS", dto.Status);
        Assert.True(dto.AllChecksPassed);

        Assert.Contains(dto.Checks, c => c.Component == "DatabaseRepository" && c.Status == "PASS");
        Assert.Contains(dto.Checks, c => c.Component == "SourceRegistry" && c.Status == "PASS");
        Assert.Contains(dto.Checks, c => c.Component == "ReliabilityEngine" && c.Status == "PASS");
        Assert.Contains(dto.Checks, c => c.Component == "OutreachSafety" && c.Status == "PASS");
    }

    [Fact]
    public async Task EmergencyKillSwitch_ImmediatelyHaltsOutboundSending()
    {
        OutreachSenderService.ResetState();
        try
        {
            // Initially kill switch is off - sends to sink
            var request = new OutreachSendRequest(
                RecipientEmail: "lead@example.com",
                RecipientName: "Lead Person",
                Subject: "Partnership Exploration",
                Body: "Hello",
                IdempotencyKey: $"IDEM-DRILL-{Guid.NewGuid():N}",
                IsTestMode: true,
                SinkAddressOverride: null
            );

            var initialResult = await _outreachSender.SendOutreachAsync(request);
            Assert.True(initialResult.Success);
            Assert.Equal("SentToTestSink", initialResult.Status);

            // Engage Kill Switch
            OutreachSenderService.IsKillSwitchEngaged = true;

            var killRequest = new OutreachSendRequest(
                RecipientEmail: "lead2@example.com",
                RecipientName: "Lead 2",
                Subject: "Urgent message",
                Body: "Hello",
                IdempotencyKey: $"IDEM-DRILL-KILL-{Guid.NewGuid():N}",
                IsTestMode: true,
                SinkAddressOverride: null
            );

            var killResult = await _outreachSender.SendOutreachAsync(killRequest);
            Assert.False(killResult.Success);
            Assert.Equal("KillSwitchEngaged", killResult.Status);
            Assert.True(killResult.Suppressed);
            Assert.Contains("Emergency kill switch is engaged", killResult.Detail);
        }
        finally
        {
            OutreachSenderService.ResetState();
        }
    }

    [Fact]
    public void KillSwitchToggleEndpoint_ProgrammaticallyEngagesAndDisengages()
    {
        OutreachSenderService.ResetState();
        try
        {
            // 1. Initial status
            var getStatusResult = _reliabilityController.GetKillSwitchStatus();
            var okInitial = Assert.IsType<OkObjectResult>(getStatusResult.Result);
            var initialJson = System.Text.Json.JsonSerializer.Serialize(okInitial.Value);
            using var initialDoc = System.Text.Json.JsonDocument.Parse(initialJson);
            Assert.False(initialDoc.RootElement.GetProperty("isEngaged").GetBoolean());

            // 2. Toggle to engaged
            var toggleResult = _reliabilityController.ToggleKillSwitch(new ToggleKillSwitchRequest(true));
            var okToggle = Assert.IsType<OkObjectResult>(toggleResult.Result);
            var toggleJson = System.Text.Json.JsonSerializer.Serialize(okToggle.Value);
            using var toggleDoc = System.Text.Json.JsonDocument.Parse(toggleJson);
            Assert.True(toggleDoc.RootElement.GetProperty("isEngaged").GetBoolean());
            Assert.Equal("KillSwitchEngaged", toggleDoc.RootElement.GetProperty("action").GetString());

            // 3. Toggle back to disengaged
            var disengageResult = _reliabilityController.ToggleKillSwitch(new ToggleKillSwitchRequest(false));
            var okDisengage = Assert.IsType<OkObjectResult>(disengageResult.Result);
            var disengageJson = System.Text.Json.JsonSerializer.Serialize(okDisengage.Value);
            using var disengageDoc = System.Text.Json.JsonDocument.Parse(disengageJson);
            Assert.False(disengageDoc.RootElement.GetProperty("isEngaged").GetBoolean());
            Assert.Equal("KillSwitchDisengaged", disengageDoc.RootElement.GetProperty("action").GetString());
        }
        finally
        {
            OutreachSenderService.ResetState();
        }
    }

    [Fact]
    public async Task EnvironmentKillSwitch_HaltsSendingWhenEnvironmentVariableSet()
    {
        OutreachSenderService.ResetState();
        try
        {
            OutreachSenderService.GetEnvironmentVariable = key => key == "OUTREACH_KILL_SWITCH" ? "true" : null;

            var request = new OutreachSendRequest(
                RecipientEmail: "emergency@example.com",
                RecipientName: "Emergency Contact",
                Subject: "Testing kill switch",
                Body: "Content",
                IdempotencyKey: $"IDEM-ENV-KILL-{Guid.NewGuid():N}",
                IsTestMode: true,
                SinkAddressOverride: null
            );

            var result = await _outreachSender.SendOutreachAsync(request);
            Assert.False(result.Success);
            Assert.Equal("KillSwitchEngaged", result.Status);
        }
        finally
        {
            OutreachSenderService.ResetState();
        }
    }
}
