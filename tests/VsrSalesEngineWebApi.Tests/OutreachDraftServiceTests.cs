using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class OutreachDraftServiceTests
{
    private readonly OutreachTemplateRepository _repo = new();
    private readonly OutreachDraftService _service;

    public OutreachDraftServiceTests()
    {
        _service = new OutreachDraftService(_repo);
    }

    [Fact]
    public async Task GetTemplatesAsync_ReturnsPreseededTemplates()
    {
        var templates = await _service.GetTemplatesAsync();

        Assert.NotEmpty(templates);
        Assert.Contains(templates, t => t.Category == "Cloud & Architecture");
        Assert.Contains(templates, t => t.Category == "Team Augmentation");
        Assert.Contains(templates, t => t.Category == "Procurement");
    }

    [Fact]
    public async Task GenerateDraftAsync_WithAllVariables_SubstitutesCorrectlyAndExtractsFacts()
    {
        var request = new DraftGenerationRequest(
            TemplateId: "TPL-001",
            ContactName: "Maya Patel",
            CompanyName: "Northstar Digital",
            Role: "Engineering Manager",
            OpportunityTitle: "Cloud Migration Architect",
            SourceEvidenceSnippet: "Public career posting for Azure DevOps migration lead",
            ServiceProposition: "cloud-native migration and enterprise API modernization",
            SenderName: "Jane Doe",
            SenderTitle: "Client Partner",
            SenderCompany: "VSR Technologies",
            JurisdictionPolicy: "US-CANSPAM"
        );

        var result = await _service.GenerateDraftAsync(request);

        Assert.NotNull(result);
        Assert.Contains("Northstar Digital", result.Subject);
        Assert.Contains("Cloud Migration Architect", result.Subject);
        Assert.Contains("Maya Patel", result.Body);
        Assert.Contains("Northstar Digital", result.Body);
        Assert.Contains("Jane Doe", result.Body);
        Assert.Contains("100 Enterprise Way", result.Body);
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
        Assert.Empty(result.ProhibitedClaims);

        // Facts used verification
        Assert.Contains(result.FactsUsed, f => f.Contains("Maya Patel"));
        Assert.Contains(result.FactsUsed, f => f.Contains("Northstar Digital"));
        Assert.Contains(result.FactsUsed, f => f.Contains("Cloud Migration Architect"));
        Assert.Contains(result.FactsUsed, f => f.Contains("Azure DevOps migration"));
    }

    [Fact]
    public async Task GenerateDraftAsync_WithEUGDPRPolicy_IncludesLegitimateInterestBasis()
    {
        var request = new DraftGenerationRequest(
            TemplateId: "TPL-004",
            ContactName: "Hans Müller",
            CompanyName: "Berlin Tech Solutions",
            Role: "Head of Infrastructure",
            OpportunityTitle: "AWS Cloud Lead",
            SourceEvidenceSnippet: "Listed publicly on corporate jobs portal",
            ServiceProposition: null,
            SenderName: null,
            SenderTitle: null,
            SenderCompany: null,
            JurisdictionPolicy: "EU-GDPR"
        );

        var result = await _service.GenerateDraftAsync(request);

        Assert.NotNull(result);
        Assert.Contains("legitimate interest", result.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UNSUBSCRIBE", result.Body);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateDraft_DetectsMissingVariables()
    {
        var request = new DraftContentValidationRequest(
            Subject: "Inquiry for {{CompanyName}}",
            Body: "Hi {{ContactName}}, we are following up on {{UnresolvedCustomTag}}.\n\nTo opt out reply UNSUBSCRIBE. VSR Technologies, 100 Enterprise Way, Austin TX 78701.",
            JurisdictionPolicy: "US-CANSPAM"
        );

        var result = _service.ValidateDraft(request);

        Assert.False(result.IsValid);
        Assert.Contains("CompanyName", result.MissingVariables);
        Assert.Contains("ContactName", result.MissingVariables);
        Assert.Contains("UnresolvedCustomTag", result.MissingVariables);
    }

    [Theory]
    [InlineData("We offer a 100% guarantee on all project completion targets.")]
    [InlineData("As the exclusive partner of your cloud provider, we can ensure instant savings.")]
    [InlineData("Our certified government contractor team can take this over.")]
    [InlineData("We built your current system so we know how to migrate it effortlessly.")]
    [InlineData("This engagement comes with zero risk for your organization.")]
    public void ValidateDraft_DetectsProhibitedClaims(string prohibitedSnippet)
    {
        var request = new DraftContentValidationRequest(
            Subject: "Partnership opportunity",
            Body: $"Hi Maya,\n\n{prohibitedSnippet}\n\nTo opt out reply UNSUBSCRIBE. VSR Technologies, 100 Enterprise Way, Austin TX 78701.",
            JurisdictionPolicy: "US-CANSPAM"
        );

        var result = _service.ValidateDraft(request);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.ProhibitedClaims);
    }

    [Fact]
    public void ValidateDraft_DetectsMissingUnsubscribeInFooter()
    {
        var request = new DraftContentValidationRequest(
            Subject: "Engineering inquiry",
            Body: "Hi Maya,\n\nWe provide cloud migrations.\n\nVSR Technologies, 100 Enterprise Way, Austin TX 78701.",
            JurisdictionPolicy: "US-CANSPAM"
        );

        var result = _service.ValidateDraft(request);

        Assert.False(result.IsValid);
        Assert.False(result.HasUnsubscribeMechanism);
        Assert.Contains(result.Warnings, w => w.Contains("opt-out"));
    }

    [Fact]
    public void ValidateDraft_DetectsMissingPhysicalAddressInFooter()
    {
        var request = new DraftContentValidationRequest(
            Subject: "Engineering inquiry",
            Body: "Hi Maya,\n\nWe provide cloud migrations.\n\nTo opt out, reply UNSUBSCRIBE. VSR Technologies.",
            JurisdictionPolicy: "US-CANSPAM"
        );

        var result = _service.ValidateDraft(request);

        Assert.False(result.IsValid);
        Assert.False(result.HasPhysicalAddress);
        Assert.Contains(result.Warnings, w => w.Contains("address"));
    }

    [Fact]
    public async Task CreateTemplateAsync_SavesNewCustomTemplate()
    {
        var dto = new CreateOutreachTemplateDto(
            Name: "Custom FinTech Migration Pitch",
            Category: "Cloud & Architecture",
            SubjectTemplate: "Modernizing core systems for {{CompanyName}}",
            BodyTemplate: "Hi {{ContactName}},\n\nWe specialize in high-throughput banking systems.\n\n{{JurisdictionFooter}}",
            ServiceProposition: "Fintech enterprise core migrations",
            JurisdictionPolicy: "US-CANSPAM",
            FooterTemplate: "Reply UNSUBSCRIBE to opt out. VSR Technologies, 100 Enterprise Way, Austin TX 78701."
        );

        var created = await _service.CreateTemplateAsync(dto, "Sales Lead");

        Assert.NotNull(created);
        Assert.StartsWith("TPL-", created.Id);
        Assert.Equal("Custom FinTech Migration Pitch", created.Name);

        var retrieved = await _service.GetTemplateByIdAsync(created.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
    }
}
