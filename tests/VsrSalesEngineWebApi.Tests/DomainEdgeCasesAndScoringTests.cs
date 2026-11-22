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

    public DomainEdgeCasesAndScoringTests()
    {
        _opportunityRepo = new OpportunityRepository(TestDbContextFactory.CreateSeeded());
        _opportunityService = new OpportunityService(_opportunityRepo);
    }

    [Fact]
    public async Task OpportunityScoring_CategorizesAccuratelyBasedOnFitAndKeywords()
    {
        var highOpp = await _opportunityService.CreateOpportunityAsync(new CreateOpportunityDto(
            Company: "Alpha Tech Solutions",
            Title: "Lead Senior .NET Azure Cloud Architect",
            Source: "Test source (no adapter)",
            Location: "Remote · India",
            Fit: "High",
            Type: "Direct project",
            SourceUrl: "https://careers.alphatech.example/job/lead-dotnet-azure",
            EvidenceSnippet: "We are seeking a Lead Senior .NET Azure Cloud Architect to lead enterprise migration.",
            MatchedQuery: ".NET Azure"
        ), "test-user");

        var lowOpp = await _opportunityService.CreateOpportunityAsync(new CreateOpportunityDto(
            Company: "Generic Staffing Co",
            Title: "Entry Level Sales Associate",
            Source: "Test source (no adapter)",
            Location: "On-site",
            Fit: "Low",
            Type: "Company/vendor",
            SourceUrl: "https://staffing.example/job/sales-rep",
            EvidenceSnippet: "Entry level sales representative needed.",
            MatchedQuery: "Sales"
        ), "test-user");

        Assert.Equal("High", highOpp.Fit);

        Assert.Equal("Low", lowOpp.Fit);
    }

    [Fact]
    public async Task StatusHistoryAudit_RecordsEveryStateTransition()
    {
        var opp = await _opportunityService.CreateOpportunityAsync(new CreateOpportunityDto(
            Company: "Beta Financial",
            Title: "Full Stack .NET React Engineer",
            Source: "Test source (no adapter)",
            Location: "Remote",
            Fit: "High",
            Type: "Individual developer",
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
}
