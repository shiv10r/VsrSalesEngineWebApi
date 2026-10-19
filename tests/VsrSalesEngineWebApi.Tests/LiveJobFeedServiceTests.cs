using VsrSalesEngineWebApi.Infrastructure.Repositories;
using VsrSalesEngineWebApi.Infrastructure.Services;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class LiveJobFeedServiceTests
{
    [Fact]
    public async Task FetchAllLiveContractJobsAsync_ReturnsLiveJobsAcrossFeeds()
    {
        var contactRepo = new ContactRepository();
        var service = new LiveJobFeedService(contactRepository: contactRepo);

        // Fetch up to 10 live contract jobs across all feeds
        var jobs = await service.FetchAllLiveContractJobsAsync(keywords: null, maxResults: 10);

        Assert.NotNull(jobs);
        Assert.NotEmpty(jobs);
        Assert.All(jobs, j =>
        {
            Assert.False(string.IsNullOrWhiteSpace(j.Id));
            Assert.False(string.IsNullOrWhiteSpace(j.Company));
            Assert.False(string.IsNullOrWhiteSpace(j.Title));
            Assert.False(string.IsNullOrWhiteSpace(j.SourceUrl));
            Assert.Contains(j.Source, new[]
            {
                "Hacker News (Freelance & Who's Hiring)",
                "We Work Remotely (Contracts)",
                "Remotive (Contract / Freelance)",
                "Jobicy (Developer Contracts)"
            });
        });
    }

    [Fact]
    public async Task FetchWeWorkRemotelyContractJobsAsync_ParsesLiveXmlRss()
    {
        var service = new LiveJobFeedService();
        var jobs = await service.FetchWeWorkRemotelyContractJobsAsync(maxResults: 5);

        Assert.NotNull(jobs);
        Assert.NotEmpty(jobs);
        Assert.All(jobs, j =>
        {
            Assert.Equal("We Work Remotely (Contracts)", j.Source);
            Assert.StartsWith("https://weworkremotely.com", j.SourceUrl);
        });
    }

    [Fact]
    public async Task FetchRemotiveContractJobsAsync_ParsesLiveJsonApi()
    {
        var service = new LiveJobFeedService();
        var jobs = await service.FetchRemotiveContractJobsAsync(maxResults: 5);

        Assert.NotNull(jobs);
        Assert.NotEmpty(jobs);
        Assert.All(jobs, j =>
        {
            Assert.Equal("Remotive (Contract / Freelance)", j.Source);
            Assert.StartsWith("https://remotive.com", j.SourceUrl);
        });
    }
}
