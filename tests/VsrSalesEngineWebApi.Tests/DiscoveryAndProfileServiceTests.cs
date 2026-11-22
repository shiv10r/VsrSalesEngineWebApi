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
        var sourceRepo = new SourceRegistryRepository(TestDbContextFactory.CreateSeeded());
        var profileRepo = new SearchProfileRepository(TestDbContextFactory.CreateSeeded());
        var oppRepoMock = new Mock<IOpportunityRepository>();

        var service = new DiscoveryService(sourceRepo, profileRepo, oppRepoMock.Object);

        var sources = await service.GetSourcesAsync();

        Assert.NotEmpty(sources);
        Assert.Contains(sources, s => s.Name.StartsWith("Hacker News"));
        Assert.DoesNotContain(sources, s => s.Name == "Company careers" || s.Name == "Public procurement");
        Assert.Contains(sources, s => s.Name == "LinkedIn handoff" && s.PolicyStatus == "Restricted");
    }

    [Fact]
    public async Task UpdateSourceStatusAsync_RejectsInvalidPolicyStatus()
    {
        var sourceRepo = new SourceRegistryRepository(TestDbContextFactory.CreateSeeded());
        var profileRepo = new SearchProfileRepository(TestDbContextFactory.CreateSeeded());
        var oppRepoMock = new Mock<IOpportunityRepository>();

        var service = new DiscoveryService(sourceRepo, profileRepo, oppRepoMock.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSourceStatusAsync("SRC-001", new UpdateSourceStatusDto("NonExistentStatus", "Invalid test")));
    }

    [Fact]
    public async Task CreateProfileAsync_RejectsEmptyNameOrKeywords()
    {
        var sourceRepo = new SourceRegistryRepository(TestDbContextFactory.CreateSeeded());
        var profileRepo = new SearchProfileRepository(TestDbContextFactory.CreateSeeded());
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
        var sourceRepo = new SourceRegistryRepository(TestDbContextFactory.CreateSeeded());
        var profileRepo = new SearchProfileRepository(TestDbContextFactory.CreateSeeded());
        var oppRepoMock = new Mock<IOpportunityRepository>();

        var service = new DiscoveryService(sourceRepo, profileRepo, oppRepoMock.Object);

        var created = await service.CreateProfileAsync(new CreateSearchProfileDto(
            Name: "Fintech .NET Engineers",
            TargetKeywords: ".NET Core, Banking, Microservices",
            ExcludedKeywords: "intern",
            Location: "India",
            OpportunityType: "Individual developer",
            PublishedWithinDays: 14,
            SelectedSources: new[] { TestFixtureData.NoAdapterSource },
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
    public async Task RunDiscovery_EnforcesPolicy_AndNeverInventsLeadsForSourcesWithoutAdapter()
    {
        var sourceRepo = new SourceRegistryRepository(TestDbContextFactory.CreateSeeded());
        var profileRepo = new SearchProfileRepository(TestDbContextFactory.CreateSeeded());
        var oppRepoMock = new Mock<IOpportunityRepository>();
        var feeds = new Mock<ILiveJobFeedService>();

        var service = new DiscoveryService(sourceRepo, profileRepo, oppRepoMock.Object, feeds.Object);

        var runResult = await service.RunDiscoveryAsync(new RunDiscoveryRequestDto(
            ProfileId: null,
            TargetKeywords: ".NET Specialist",
            ExcludedKeywords: null,
            Location: "India",
            SelectedSources: new[] { TestFixtureData.NoAdapterSource, "LinkedIn handoff" },
            MaxResults: 20
        ), "Sales Member");

        Assert.Equal("Completed", runResult.Status);

        var linkedIn = runResult.SourceMetrics.Single(m => m.SourceName == "LinkedIn handoff");
        Assert.Equal("PolicyRestricted", linkedIn.Status);
        Assert.Equal(1, linkedIn.RejectedByPolicy);

        var noAdapter = runResult.SourceMetrics.Single(m => m.SourceName == TestFixtureData.NoAdapterSource);
        Assert.Equal("NoAdapter", noAdapter.Status);
        Assert.Equal(0, noAdapter.RecordsFetched);
        Assert.Contains($"no adapter for {TestFixtureData.NoAdapterSource}", noAdapter.Note);

        Assert.Equal(0, runResult.EvaluatedCount);
        Assert.StartsWith("Nothing to do", runResult.Summary);
        oppRepoMock.Verify(r => r.AddAsync(It.IsAny<Opportunity>(), It.IsAny<CancellationToken>()), Times.Never);
        feeds.Verify(f => f.FetchAllLiveContractJobsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunDiscovery_WithNoSourceSelected_SaysNothingToDo()
    {
        var service = new DiscoveryService(
            new SourceRegistryRepository(TestDbContextFactory.CreateDefaultSeeded()),
            new SearchProfileRepository(TestDbContextFactory.CreateDefaultSeeded()),
            new Mock<IOpportunityRepository>().Object);

        var result = await service.RunDiscoveryAsync(new RunDiscoveryRequestDto(null, null, null, null, null, null), "Tester");

        Assert.Empty(result.SourceMetrics);
        Assert.Equal("Nothing to do: no source is selected.", result.Summary);
    }

    [Fact]
    public async Task RunDiscovery_DisabledLiveFeed_IsNotFetched()
    {
        var feeds = new Mock<ILiveJobFeedService>();
        var service = new DiscoveryService(
            new SourceRegistryRepository(TestDbContextFactory.CreateDefaultSeeded()),
            new SearchProfileRepository(TestDbContextFactory.CreateDefaultSeeded()),
            new Mock<IOpportunityRepository>().Object, feeds.Object);

        var result = await service.RunDiscoveryAsync(new RunDiscoveryRequestDto(
            null, null, null, null, new[] { "Hacker News (Freelance & Who's Hiring)" }, null), "Tester");

        Assert.Equal("PolicyRestricted", result.SourceMetrics.Single().Status);
        feeds.Verify(f => f.FetchAllLiveContractJobsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
