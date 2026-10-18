using Moq;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class DiscoveryAndProfileServiceTests
{
    [Fact]
    public async Task GetSourcesAsync_ReturnsBaselineConfiguredSources()
    {
        var sourceRepo = new SourceRegistryRepository();
        var profileRepo = new SearchProfileRepository();
        var oppRepoMock = new Mock<IOpportunityRepository>();

        var service = new DiscoveryService(sourceRepo, profileRepo, oppRepoMock.Object);

        var sources = await service.GetSourcesAsync();

        Assert.NotEmpty(sources);
        Assert.Contains(sources, s => s.Name == "Company careers");
        Assert.Contains(sources, s => s.Name == "LinkedIn handoff" && s.PolicyStatus == "Restricted");
    }

    [Fact]
    public async Task UpdateSourceStatusAsync_RejectsInvalidPolicyStatus()
    {
        var sourceRepo = new SourceRegistryRepository();
        var profileRepo = new SearchProfileRepository();
        var oppRepoMock = new Mock<IOpportunityRepository>();

        var service = new DiscoveryService(sourceRepo, profileRepo, oppRepoMock.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSourceStatusAsync("SRC-001", new UpdateSourceStatusDto("NonExistentStatus", "Invalid test")));
    }

    [Fact]
    public async Task CreateProfileAsync_RejectsEmptyNameOrKeywords()
    {
        var sourceRepo = new SourceRegistryRepository();
        var profileRepo = new SearchProfileRepository();
        var oppRepoMock = new Mock<IOpportunityRepository>();

        var service = new DiscoveryService(sourceRepo, profileRepo, oppRepoMock.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateProfileAsync(new CreateSearchProfileDto("", "C#", null, "Remote", null, null, new[] { "Company careers" }, 50), "Tester"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateProfileAsync(new CreateSearchProfileDto(".NET Leads", "", null, "Remote", null, null, new[] { "Company careers" }, 50), "Tester"));
    }

    [Fact]
    public async Task CreateAndLifecycle_Profile_WorksCorrectly()
    {
        var sourceRepo = new SourceRegistryRepository();
        var profileRepo = new SearchProfileRepository();
        var oppRepoMock = new Mock<IOpportunityRepository>();

        var service = new DiscoveryService(sourceRepo, profileRepo, oppRepoMock.Object);

        var created = await service.CreateProfileAsync(new CreateSearchProfileDto(
            Name: "Fintech .NET Engineers",
            TargetKeywords: ".NET Core, Banking, Microservices",
            ExcludedKeywords: "intern",
            Location: "India",
            OpportunityType: "Individual developer",
            PublishedWithinDays: 14,
            SelectedSources: new[] { "Company careers", "Public job board" },
            MaxResults: 30
        ), "Tester");

        Assert.NotNull(created.Id);
        Assert.Equal("Fintech .NET Engineers", created.Name);

        var updated = await service.UpdateProfileAsync(created.Id, new UpdateSearchProfileDto(
            Name: "Fintech .NET & Cloud Engineers",
            TargetKeywords: null,
            ExcludedKeywords: null,
            Location: null,
            OpportunityType: null,
            PublishedWithinDays: null,
            SelectedSources: null,
            MaxResults: 50,
            IsActive: null
        ), "Tester");

        Assert.NotNull(updated);
        Assert.Equal("Fintech .NET & Cloud Engineers", updated.Name);
        Assert.Equal(50, updated.MaxResults);

        var deleted = await service.DeleteProfileAsync(created.Id, "Tester");
        Assert.True(deleted);

        var notFound = await service.GetProfileByIdAsync(created.Id);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task RunDiscovery_EnforcesPolicy_RestrictsLinkedInAndDeduplicates()
    {
        var sourceRepo = new SourceRegistryRepository();
        var profileRepo = new SearchProfileRepository();
        var oppRepoMock = new Mock<IOpportunityRepository>();

        oppRepoMock.Setup(r => r.GetByDeduplicationKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Opportunity?)null);

        var service = new DiscoveryService(sourceRepo, profileRepo, oppRepoMock.Object);

        var runResult = await service.RunDiscoveryAsync(new RunDiscoveryRequestDto(
            ProfileId: null,
            TargetKeywords: ".NET Specialist",
            ExcludedKeywords: null,
            Location: "India · Remote",
            SelectedSources: new[] { "Company careers", "LinkedIn handoff", "Public procurement" },
            MaxResults: 20
        ), "Sales Member");

        Assert.NotNull(runResult.RunId);
        Assert.Equal("Completed", runResult.Status);

        // LinkedIn handoff is Restricted by policy and must be flagged as PolicyRestricted
        var linkedInMetric = runResult.SourceMetrics.FirstOrDefault(m => m.SourceName == "LinkedIn handoff");
        Assert.NotNull(linkedInMetric);
        Assert.Equal("PolicyRestricted", linkedInMetric.Status);
        Assert.Equal(1, linkedInMetric.RejectedByPolicy);

        // Active sources should fetch
        var careersMetric = runResult.SourceMetrics.FirstOrDefault(m => m.SourceName == "Company careers");
        Assert.NotNull(careersMetric);
        Assert.Equal("Success", careersMetric.Status);
        Assert.True(careersMetric.NewMatches > 0);

        // Verify repository add was called for new candidates
        oppRepoMock.Verify(r => r.AddAsync(It.IsAny<Opportunity>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
