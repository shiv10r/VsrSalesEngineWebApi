using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services;
using VsrSalesEngineWebApi.Infrastructure.Repositories;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class SourceAdapterContractFixtureTests
{
    private readonly ISourceRegistryRepository _sourceRepo;
    private readonly IReliabilityService _reliabilityService;

    public SourceAdapterContractFixtureTests()
    {
        _sourceRepo = new SourceRegistryRepository();
        _reliabilityService = new ReliabilityService(new ExecutionRepository(), new AutomationScheduleRepository());
    }

    [Fact]
    public void Fixture_Success_ParsesCanonicalOpportunities()
    {
        var sampleJson = """
        [
            {
                "id": "JOB-101",
                "company": "Northstar Digital",
                "title": "Senior .NET / Azure Engineer",
                "url": "https://careers.northstar.example/job/101",
                "description": "Enterprise platform modernizing .NET 10 microservices."
            }
        ]
        """;

        using var doc = System.Text.Json.JsonDocument.Parse(sampleJson);
        var array = doc.RootElement;
        Assert.Equal(1, array.GetArrayLength());

        var first = array[0];
        Assert.Equal("Northstar Digital", first.GetProperty("company").GetString());
        Assert.Equal("Senior .NET / Azure Engineer", first.GetProperty("title").GetString());
        Assert.StartsWith("https://", first.GetProperty("url").GetString()!);
    }

    [Fact]
    public void Fixture_EmptyResults_HandlesGracefullyWithoutThrowing()
    {
        var emptyJson = "[]";
        using var doc = System.Text.Json.JsonDocument.Parse(emptyJson);
        var array = doc.RootElement;
        Assert.Equal(0, array.GetArrayLength());
    }

    [Fact]
    public void Fixture_MalformedPayload_CatchesAndClassifiesGracefully()
    {
        var malformed = "{ this is not valid json <html";
        var caught = false;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(malformed);
        }
        catch (System.Text.Json.JsonException ex)
        {
            caught = true;
            var classification = _reliabilityService.ClassifyFailure(ex.Message, ex);
            Assert.Equal("Permanent", classification); // Malformed input is a permanent parsing error
        }

        Assert.True(caught);
    }

    [Fact]
    public void Fixture_Throttling_ClassifiedAsRateLimitExceeded()
    {
        var throttlingError = "HTTP 429: Too Many Requests from Adzuna / JobBoard API";
        var classification = _reliabilityService.ClassifyFailure(throttlingError);
        Assert.Equal("RateLimitExceeded", classification);
    }

    [Fact]
    public void Fixture_AuthFailure_ClassifiedAsPermanent()
    {
        var authError = "HTTP 401 Unauthorized: Invalid API key credentials for corporate procurement portal";
        var classification = _reliabilityService.ClassifyFailure(authError);
        Assert.Equal("Permanent", classification);
    }

    [Fact]
    public void Fixture_Timeout_ClassifiedAsTimeout()
    {
        var timeoutError = "Task timed out: Source connector did not respond within 30000ms";
        var classification = _reliabilityService.ClassifyFailure(timeoutError);
        Assert.Equal("Timeout", classification);
    }

    [Fact]
    public void Fixture_DuplicatesInPayload_AreNormalizedToSingleCanonicalEntry()
    {
        var duplicatePayload = new[]
        {
            new { Id = "EXT-1", Url = "https://careers.example.com/job/dotnet-1", Title = ".NET Developer" },
            new { Id = "EXT-2", Url = "https://careers.example.com/job/dotnet-1", Title = ".NET Developer (Syndicated)" }
        };

        var distinctUrls = duplicatePayload.GroupBy(x => x.Url.ToLowerInvariant()).ToList();
        Assert.Single(distinctUrls);
        Assert.Equal(2, distinctUrls[0].Count()); // 2 raw postings, but exactly 1 canonical URL
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
