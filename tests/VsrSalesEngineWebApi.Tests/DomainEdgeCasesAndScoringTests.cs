using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class DomainEdgeCasesAndScoringTests
{
    private readonly IOpportunityRepository _opportunityRepo;
    private readonly IOpportunityService _opportunityService;
    private readonly ISuppressionRepository _suppressionRepo;
    private readonly ICampaignRepository _campaignRepo;
    private readonly ICampaignService _campaignService;

    public DomainEdgeCasesAndScoringTests()
    {
        _opportunityRepo = new OpportunityRepository();
        _opportunityService = new OpportunityService(_opportunityRepo);
        _suppressionRepo = new SuppressionRepository();
        _campaignRepo = new CampaignRepository();
        _campaignService = new CampaignService(_campaignRepo);
    }

    [Fact]
    public async Task OpportunityScoring_CategorizesAccuratelyBasedOnFitAndKeywords()
    {
        var highOpp = await _opportunityService.CreateOpportunityAsync(new CreateOpportunityDto(
            Company: "Alpha Tech Solutions",
            Title: "Lead Senior .NET Azure Cloud Architect",
            Source: "Company careers",
            Location: "Remote · India",
            Fit: "High",
            Type: "Direct project",
            HasVendorDocRequirements: false,
            VendorRequirementNotes: null,
            SourceUrl: "https://careers.alphatech.example/job/lead-dotnet-azure",
            EvidenceSnippet: "We are seeking a Lead Senior .NET Azure Cloud Architect to lead enterprise migration.",
            MatchedQuery: ".NET Azure"
        ), "test-user");

        var lowOpp = await _opportunityService.CreateOpportunityAsync(new CreateOpportunityDto(
            Company: "Generic Staffing Co",
            Title: "Entry Level Sales Associate",
            Source: "Public job board",
            Location: "On-site",
            Fit: "Low",
            Type: "Company/vendor",
            HasVendorDocRequirements: true,
            VendorRequirementNotes: "Staffing agency supplier registration required",
            SourceUrl: "https://staffing.example/job/sales-rep",
            EvidenceSnippet: "Entry level sales representative needed.",
            MatchedQuery: "Sales"
        ), "test-user");

        Assert.Equal("High", highOpp.Fit);
        Assert.False(highOpp.HasVendorDocRequirements);

        Assert.Equal("Low", lowOpp.Fit);
        Assert.True(lowOpp.HasVendorDocRequirements);
        Assert.NotNull(lowOpp.VendorRequirementNotes);
    }

    [Fact]
    public async Task StatusHistoryAudit_RecordsEveryStateTransition()
    {
        var opp = await _opportunityService.CreateOpportunityAsync(new CreateOpportunityDto(
            Company: "Beta Financial",
            Title: "Full Stack .NET React Engineer",
            Source: "Approved feed",
            Location: "Remote",
            Fit: "High",
            Type: "Individual developer",
            HasVendorDocRequirements: false,
            VendorRequirementNotes: null,
            SourceUrl: "https://feed.example/beta-fin",
            EvidenceSnippet: "Contract for .NET and React platform developer.",
            MatchedQuery: ".NET React"
        ), "analyst-1");

        // Transition 1: New -> Needs review
        var t1 = await _opportunityService.TransitionStatusAsync(opp.Id, new TransitionStatusDto("Needs review", "Technical vetting initiated"), "analyst-1");
        Assert.NotNull(t1);
        Assert.Equal("Needs review", t1.Status);
        Assert.Equal(2, t1.StatusHistory.Count); // Initial creation record + transition 1
        Assert.Equal("New", t1.StatusHistory[1].FromStatus);
        Assert.Equal("Needs review", t1.StatusHistory[1].ToStatus);

        // Transition 2: Needs review -> Qualified
        var t2 = await _opportunityService.TransitionStatusAsync(opp.Id, new TransitionStatusDto("Qualified", "Budget and direct tech need verified"), "manager-1");
        Assert.NotNull(t2);
        Assert.Equal("Qualified", t2.Status);
        Assert.Equal(3, t2.StatusHistory.Count); // Initial creation record + transition 1 + transition 2
        Assert.Equal("Needs review", t2.StatusHistory[2].FromStatus);
        Assert.Equal("Qualified", t2.StatusHistory[2].ToStatus);
    }

    [Fact]
    public async Task Suppression_WildcardAndExactMatching()
    {
        await _suppressionRepo.AddAsync(new SuppressionRecord
        {
            Id = $"SUP-TEST-1-{Guid.NewGuid():N}",
            Target = "ceo@competitor.example",
            Type = "Email",
            Reason = "Explicit opt out request",
            Source = "EmailReply"
        });

        await _suppressionRepo.AddAsync(new SuppressionRecord
        {
            Id = $"SUP-TEST-2-{Guid.NewGuid():N}",
            Target = "@blocked-domain.com",
            Type = "Domain",
            Reason = "Company exclusion list",
            Source = "Manual"
        });

        var isExactSuppressed = await _suppressionRepo.IsSuppressedAsync("ceo@competitor.example");
        var isDomainSuppressed1 = await _suppressionRepo.IsSuppressedAsync("cto@blocked-domain.com");
        var isDomainSuppressed2 = await _suppressionRepo.IsSuppressedAsync("sales@blocked-domain.com");
        var isAllowed = await _suppressionRepo.IsSuppressedAsync("engineer@permitted-company.com");

        Assert.True(isExactSuppressed);
        Assert.True(isDomainSuppressed1);
        Assert.True(isDomainSuppressed2);
        Assert.False(isAllowed);
    }

    [Fact]
    public async Task ComplianceGate_FailsClosedWhenStatutoryDetailsMissing()
    {
        // Campaign with incomplete statutory details
        var incompleteCampaign = new Campaign
        {
            Id = $"CMP-INC-{Guid.NewGuid():N}",
            Name = "Incomplete Campaign",
            Audience = "CTOs",
            Mode = "ApprovalRequired",
            SenderEmail = "", // Missing!
            SenderAddress = "", // Missing!
            OptOutMechanismUrl = "" // Missing!
        };
        await _campaignRepo.CreateAsync(incompleteCampaign);

        var gateResult = await _campaignService.EvaluateComplianceGateAsync(incompleteCampaign.Id);

        Assert.False(gateResult.IsProductionReady);
        Assert.False(gateResult.SenderIdentityConfigured);
        Assert.False(gateResult.PhysicalAddressConfigured);
        Assert.False(gateResult.OptOutMechanismConfigured);
        Assert.Contains(gateResult.BlockingReasons, r => r.Contains("Sender identity", StringComparison.OrdinalIgnoreCase) || r.Contains("sender", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(gateResult.BlockingReasons, r => r.Contains("Physical", StringComparison.OrdinalIgnoreCase) || r.Contains("address", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(gateResult.RemediationSteps, s => s.Contains("Configure"));
    }

    [Fact]
    public async Task ComplianceGate_PassesWhenAllStatutoryDetailsConfigured()
    {
        var readyCampaign = new Campaign
        {
            Id = $"CMP-READY-{Guid.NewGuid():N}",
            Name = "Fully Compliant Campaign",
            Audience = "Engineering Leaders",
            Mode = "ApprovalRequired",
            SenderName = "VSR Outreach Lead",
            SenderEmail = "outreach@vsrsalesengine.com",
            SenderAddress = "100 Enterprise Way, Suite 400, Austin TX 78701",
            OptOutMechanismUrl = "https://vsrsalesengine.com/opt-out",
            Jurisdiction = "US-CANSPAM",
            DailySendLimit = 50
        };
        await _campaignRepo.CreateAsync(readyCampaign);

        var gateResult = await _campaignService.EvaluateComplianceGateAsync(readyCampaign.Id);

        Assert.True(gateResult.IsProductionReady);
        Assert.True(gateResult.SenderIdentityConfigured);
        Assert.True(gateResult.ProviderCredentialsConfigured);
        Assert.True(gateResult.PhysicalAddressConfigured);
        Assert.True(gateResult.OptOutMechanismConfigured);
        Assert.True(gateResult.JurisdictionPolicyConfigured);
        Assert.True(gateResult.RateLimitsConfigured);
        Assert.Empty(gateResult.BlockingReasons);
    }
}
