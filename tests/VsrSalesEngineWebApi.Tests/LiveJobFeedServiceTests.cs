using System.Net;
using System.Text;
using VsrSalesEngineWebApi.Infrastructure.Services;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

/// <summary>The feed parsers run against saved sample payloads served by a fake handler: no real network.</summary>
public class LiveJobFeedServiceTests
{
    private const string RemotiveSample = """
    {"jobs":[
      {"company_name":"Northwind","title":"Senior .NET Contractor","job_type":"contract","url":"https://remotive.com/remote-jobs/software-dev/senior-net-1",
       "description":"<p>Contract work on a <b>.NET</b> API. Contact hiring@northwind.example</p>","candidate_required_location":"Worldwide"},
      {"company_name":"","title":"Missing company","job_type":"full_time","url":"https://remotive.com/remote-jobs/x","description":"x"}
    ]}
    """;

    private const string WwrSample = """
    <?xml version="1.0" encoding="UTF-8"?>
    <rss version="2.0"><channel>
      <item><title>Acme Corp: Freelance React Developer</title>
        <link>https://weworkremotely.com/remote-jobs/acme-react</link>
        <description>&lt;p&gt;Freelance React work for a small team.&lt;/p&gt;</description>
        <pubDate>Mon, 03 Nov 2025 10:00:00 +0000</pubDate></item>
    </channel></rss>
    """;

    private sealed class SampleHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add(url);

            if (url.StartsWith("https://remotive.com/")) return Task.FromResult(Ok(RemotiveSample, "application/json"));
            if (url.StartsWith("https://weworkremotely.com/")) return Task.FromResult(Ok(WwrSample, "application/rss+xml"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Ok(string body, string type) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, type) };
    }

    private static (LiveJobFeedService Service, SampleHandler Handler) Build()
    {
        var handler = new SampleHandler();
        return (new LiveJobFeedService(new HttpClient(handler)), handler);
    }

    [Fact]
    public async Task Remotive_ParsesTheSampleJson_AndSkipsRowsWithoutACompany()
    {
        var (service, _) = Build();

        var jobs = await service.FetchRemotiveContractJobsAsync(maxResults: 5);

        var job = Assert.Single(jobs);
        Assert.Equal("Remotive (Contract / Freelance)", job.Source);
        Assert.Equal("Northwind", job.Company);
        Assert.Equal("Senior .NET Contractor", job.Title);
        Assert.Equal("https://remotive.com/remote-jobs/software-dev/senior-net-1", job.SourceUrl);
        Assert.Contains("hiring@northwind.example", job.EvidenceSnippet);
        Assert.DoesNotContain("<p>", job.EvidenceSnippet);
    }

    [Fact]
    public async Task WeWorkRemotely_ParsesTheSampleRss_IntoCompanyAndTitle()
    {
        var (service, _) = Build();

        var jobs = await service.FetchWeWorkRemotelyContractJobsAsync(maxResults: 5);

        var job = jobs.First();
        Assert.Equal("We Work Remotely (Contracts)", job.Source);
        Assert.Equal("Acme Corp", job.Company);
        Assert.Equal("Freelance React Developer", job.Title);
        Assert.StartsWith("https://weworkremotely.com", job.SourceUrl);
    }

    [Fact]
    public async Task FetchAll_CombinesFeeds_AndAFailingFeedDoesNotBreakTheRest()
    {
        var (service, handler) = Build();

        var jobs = await service.FetchAllLiveContractJobsAsync(keywords: null, maxResults: 10);

        Assert.NotEmpty(jobs);
        Assert.All(jobs, j =>
        {
            Assert.False(string.IsNullOrWhiteSpace(j.Id));
            Assert.False(string.IsNullOrWhiteSpace(j.Company));
            Assert.False(string.IsNullOrWhiteSpace(j.SourceUrl));
        });
        Assert.Contains(jobs, j => j.Source.StartsWith("Remotive"));
        Assert.Contains(jobs, j => j.Source.StartsWith("We Work Remotely"));
        Assert.NotEmpty(handler.Requests);
    }
}
