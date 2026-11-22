using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class SourceAdapterContractFixtureTests
{
    private readonly ISourceRegistryRepository _sourceRepo;

    public SourceAdapterContractFixtureTests()
    {
        _sourceRepo = new SourceRegistryRepository(TestDbContextFactory.CreateSeeded());
    }

    [Fact]
    public async Task Fixture_RestrictedPolicy_BlocksAutomatedDirectFetch()
    {
        var restrictedSource = await _sourceRepo.GetSourceByIdAsync("SRC-006"); // LinkedIn handoff
        Assert.NotNull(restrictedSource);
        Assert.Equal("Restricted", restrictedSource.PolicyStatus);
        Assert.False(restrictedSource.AllowsDirectFetch);
        Assert.Equal(0, restrictedSource.DailyRequestLimit);
    }
}
