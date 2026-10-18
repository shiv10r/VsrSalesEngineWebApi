using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class CampaignServiceTests
{
    private readonly CampaignRepository _repo = new();
    private readonly CampaignService _service;

    public CampaignServiceTests()
    {
        _service = new CampaignService(_repo);
    }

    [Fact]
    public async Task GetCampaignsAsync_ReturnsSeededCampaigns()
    {
        var campaigns = await _service.GetCampaignsAsync();

        Assert.NotEmpty(campaigns);
        Assert.Contains(campaigns, c => c.Name.Contains(".NET API Modernization"));
    }

    [Fact]
    public async Task CreateCampaignAsync_CreatesWithDefaultComplianceParameters()
    {
        var dto = new CreateCampaignDto(
            Name: "Enterprise Cloud Modernization",
            Audience: "VP of Engineering & Directors",
            Mode: "DraftOnly",
            DailySendLimit: 25,
            MaxTouches: 3,
            DelayDaysBetweenTouches: 3,
            SelectedTemplateId: "TPL-001",
            SenderName: "VSR Outreach Lead",
            SenderEmail: "outreach@vsrtechnologies.com",
            SenderAddress: "100 Enterprise Way, Suite 400, Austin TX 78701",
            Jurisdiction: "US-CANSPAM",
            OptOutMechanismUrl: "https://vsrtechnologies.com/outreach/opt-out"
        );

        var created = await _service.CreateCampaignAsync(dto, "Admin User");

        Assert.NotNull(created);
        Assert.StartsWith("CMP-", created.Id);
        Assert.Equal("Draft", created.Status);
        Assert.Equal("DraftOnly", created.Mode);
        Assert.Equal(25, created.DailySendLimit);
    }

    [Fact]
    public async Task EvaluateComplianceGateAsync_ValidCampaign_PassesGate()
    {
        var gate = await _service.EvaluateComplianceGateAsync("CMP-001");

        Assert.NotNull(gate);
        Assert.True(gate.IsProductionReady);
        Assert.True(gate.SenderIdentityConfigured);
        Assert.True(gate.PhysicalAddressConfigured);
        Assert.True(gate.OptOutMechanismConfigured);
        Assert.True(gate.JurisdictionPolicyConfigured);
        Assert.True(gate.RateLimitsConfigured);
        Assert.Empty(gate.BlockingReasons);
    }

    [Fact]
    public async Task EvaluateComplianceGateAsync_MissingPostalAddress_FailsGateWithRemediation()
    {
        var invalid = new Campaign
        {
            Id = $"CMP-INVALID-{Guid.NewGuid():N}",
            Name = "Incomplete Campaign",
            Audience = "Prospects",
            Mode = "DraftOnly",
            Status = "Draft",
            SenderName = "VSR Rep",
            SenderEmail = "rep@vsr.example",
            SenderAddress = "", // Missing physical address
            Jurisdiction = "US-CANSPAM",
            OptOutMechanismUrl = "https://example.com/optout",
            DailySendLimit = 20
        };
        await _repo.CreateAsync(invalid);

        var gate = await _service.EvaluateComplianceGateAsync(invalid.Id);

        Assert.False(gate.IsProductionReady);
        Assert.False(gate.PhysicalAddressConfigured);
        Assert.NotEmpty(gate.BlockingReasons);
        Assert.NotEmpty(gate.RemediationSteps);
        Assert.Contains(gate.RemediationSteps, r => r.Contains("postal address"));
    }

    [Fact]
    public async Task UpdateStatusAsync_ActivatingAutoSendWhenGateFails_ThrowsException()
    {
        var incomplete = new Campaign
        {
            Id = $"CMP-INCOMP-{Guid.NewGuid():N}",
            Name = "Non-Compliant AutoSend Campaign",
            Audience = "Prospects",
            Mode = "AutoSendWithinRules",
            Status = "Draft",
            SenderName = "", // Incomplete
            SenderEmail = "", // Incomplete
            SenderAddress = "",
            DailySendLimit = 20
        };
        await _repo.CreateAsync(incomplete);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateStatusAsync(incomplete.Id, "Active", "Manual activation attempt"));
    }

    [Fact]
    public async Task EvaluateSequenceStepAsync_WhenContactReplied_StopsSequence()
    {
        var check = new ContactSequenceCheckRequest(
            ContactEmail: "lead@prospect.example",
            CurrentTouches: 1,
            HasReplied: true,
            HasOptedOut: false,
            HasBounced: false
        );

        var result = await _service.EvaluateSequenceStepAsync("CMP-001", check);

        Assert.False(result.CanSendNextStep);
        Assert.Equal("StoppedOnReply", result.Status);
        Assert.Contains("Contact replied", result.Reason);
    }

    [Fact]
    public async Task EvaluateSequenceStepAsync_WhenContactOptedOut_StopsSequence()
    {
        var check = new ContactSequenceCheckRequest(
            ContactEmail: "optout@prospect.example",
            CurrentTouches: 1,
            HasReplied: false,
            HasOptedOut: true,
            HasBounced: false
        );

        var result = await _service.EvaluateSequenceStepAsync("CMP-001", check);

        Assert.False(result.CanSendNextStep);
        Assert.Equal("StoppedOnOptOut", result.Status);
        Assert.Contains("opted out", result.Reason);
    }

    [Fact]
    public async Task EvaluateSequenceStepAsync_WhenMaxTouchesReached_CompletesSequence()
    {
        var check = new ContactSequenceCheckRequest(
            ContactEmail: "prospect@lead.example",
            CurrentTouches: 3, // CMP-001 max touches is 3
            HasReplied: false,
            HasOptedOut: false,
            HasBounced: false
        );

        var result = await _service.EvaluateSequenceStepAsync("CMP-001", check);

        Assert.False(result.CanSendNextStep);
        Assert.Equal("MaxTouchesReached", result.Status);
    }

    [Fact]
    public async Task EvaluateSequenceStepAsync_WhenEligible_ReturnsNextStepAndConfiguredDelay()
    {
        var check = new ContactSequenceCheckRequest(
            ContactEmail: "eligible@lead.example",
            CurrentTouches: 1,
            HasReplied: false,
            HasOptedOut: false,
            HasBounced: false
        );

        var result = await _service.EvaluateSequenceStepAsync("CMP-001", check);

        Assert.True(result.CanSendNextStep);
        Assert.Equal(2, result.NextStepNumber);
        Assert.Equal(3, result.RecommendedDelayDays); // CMP-001 delay is 3 days
        Assert.Equal("EligibleForFollowUp", result.Status);
    }
}
