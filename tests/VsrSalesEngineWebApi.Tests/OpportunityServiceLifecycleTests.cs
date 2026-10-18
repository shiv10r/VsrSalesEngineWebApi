using Moq;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class OpportunityServiceLifecycleTests
{
    private readonly Mock<IOpportunityRepository> _repoMock = new();

    [Fact]
    public async Task CreateOpportunityAsync_ValidInput_CreatesWithProvenanceAndInitialStatusHistory()
    {
        // Arrange
        _repoMock.Setup(r => r.GetByDeduplicationKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Opportunity?)null);

        Opportunity? savedEntity = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Opportunity>(), It.IsAny<CancellationToken>()))
            .Callback<Opportunity, CancellationToken>((o, _) => savedEntity = o)
            .ReturnsAsync((Opportunity o, CancellationToken _) => o);

        var service = new OpportunityService(_repoMock.Object);

        var dto = new CreateOpportunityDto(
            Company: "Acme Cloud Corp",
            Title: "Staff .NET Engineer Contract",
            Source: "Company careers",
            Location: "Remote",
            Fit: "High",
            Type: "Individual developer",
            HasVendorDocRequirements: false,
            VendorRequirementNotes: null,
            SourceUrl: "https://acme.example/careers/staff-dotnet",
            EvidenceSnippet: "Looking for senior architect to guide cloud migration.",
            MatchedQuery: ".net engineer"
        );

        // Act
        var result = await service.CreateOpportunityAsync(dto, "Test Recruiter");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Acme Cloud Corp", result.Company);
        Assert.Equal("New", result.Status);
        Assert.Single(result.StatusHistory);
        Assert.Equal("Test Recruiter", result.StatusHistory[0].ChangedBy);
        Assert.Equal("New", result.StatusHistory[0].ToStatus);
        Assert.NotNull(savedEntity);
        Assert.Equal("acme cloud corp|staff .net engineer contract", savedEntity.DeduplicationKey);
    }

    [Theory]
    [InlineData("", "Valid Title")]
    [InlineData("Valid Company", "")]
    [InlineData("   ", "   ")]
    public async Task CreateOpportunityAsync_InvalidFields_ThrowsArgumentException(string company, string title)
    {
        var service = new OpportunityService(_repoMock.Object);
        var dto = new CreateOpportunityDto(company, title, "Source", "Location", "High", "Individual developer", false, null, null, null, null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateOpportunityAsync(dto));
    }

    [Fact]
    public async Task CreateOpportunityAsync_DuplicateLead_PerformsIdempotentUpdate()
    {
        var existing = new Opportunity
        {
            Id = "OP-1001",
            Company = "TechVentures",
            Title = "Backend API Lead",
            DeduplicationKey = "techventures|backend api lead",
            RetrievedAtUtc = DateTime.UtcNow.AddDays(-2)
        };

        _repoMock.Setup(r => r.GetByDeduplicationKeyAsync("techventures|backend api lead", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = new OpportunityService(_repoMock.Object);
        var dto = new CreateOpportunityDto("TechVentures", "Backend API Lead", "Feed", "Remote", "High", "Individual developer", false, null, null, null, null);

        var result = await service.CreateOpportunityAsync(dto, "Harvester");

        Assert.Equal("OP-1001", result.Id);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Opportunity>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransitionStatusAsync_ValidStatus_RecordsAuditHistory()
    {
        var existing = new Opportunity
        {
            Id = "OP-2002",
            Company = "Alpha Health",
            Title = "Full-stack Developer",
            Status = "New",
            StatusHistory = new List<OpportunityStatusChange>()
        };

        _repoMock.Setup(r => r.GetByIdAsync("OP-2002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = new OpportunityService(_repoMock.Object);

        var result = await service.TransitionStatusAsync("OP-2002", new TransitionStatusDto("Qualified", "Client budget confirmed"), "Lead Partner");

        Assert.NotNull(result);
        Assert.Equal("Qualified", result.Status);
        Assert.Single(result.StatusHistory);
        Assert.Equal("New", result.StatusHistory[0].FromStatus);
        Assert.Equal("Qualified", result.StatusHistory[0].ToStatus);
        Assert.Equal("Lead Partner", result.StatusHistory[0].ChangedBy);
        Assert.Equal("Client budget confirmed", result.StatusHistory[0].Reason);
    }

    [Fact]
    public async Task TransitionStatusAsync_InvalidStatus_ThrowsArgumentException()
    {
        var service = new OpportunityService(_repoMock.Object);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TransitionStatusAsync("OP-2002", new TransitionStatusDto("NonExistentStatus", null)));
    }

    [Fact]
    public async Task ArchiveOpportunityAsync_ExistingRecord_SetsStatusToArchived()
    {
        var existing = new Opportunity
        {
            Id = "OP-3003",
            Company = "Beta Corp",
            Title = "DevOps Engineer",
            Status = "Qualified",
            StatusHistory = new List<OpportunityStatusChange>()
        };

        _repoMock.Setup(r => r.GetByIdAsync("OP-3003", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = new OpportunityService(_repoMock.Object);
        var success = await service.ArchiveOpportunityAsync("OP-3003", "Admin");

        Assert.True(success);
        Assert.Equal("Archived", existing.Status);
        Assert.Single(existing.StatusHistory);
        Assert.Equal("Archived", existing.StatusHistory[0].ToStatus);
    }
}
