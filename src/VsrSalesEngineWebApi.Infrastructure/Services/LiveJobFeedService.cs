using Microsoft.Extensions.Logging;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Infrastructure.Services.LiveFeeds;

namespace VsrSalesEngineWebApi.Infrastructure.Services;

/// <summary>Combines the individual live job feeds behind <see cref="ILiveJobFeedService"/>.</summary>
public class LiveJobFeedService : ILiveJobFeedService
{
    private readonly HackerNewsFeed _hackerNews;
    private readonly WeWorkRemotelyFeed _weWorkRemotely;
    private readonly RemotiveFeed _remotive;
    private readonly JobicyFeed _jobicy;

    public LiveJobFeedService(
        HttpClient? httpClient = null,
        IContactRepository? contactRepository = null,
        ILogger<LiveJobFeedService>? logger = null)
    {
        var client = httpClient ?? new HttpClient();
        if (!client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 VsrSalesEngine/1.0");
        }
        if (client.Timeout == TimeSpan.FromSeconds(100))
        {
            client.Timeout = TimeSpan.FromSeconds(12);
        }

        _hackerNews = new HackerNewsFeed(client, logger, contactRepository);
        _weWorkRemotely = new WeWorkRemotelyFeed(client, logger);
        _remotive = new RemotiveFeed(client, logger);
        _jobicy = new JobicyFeed(client, logger);
    }

    public Task<IReadOnlyList<Opportunity>> FetchHackerNewsContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default)
        => _hackerNews.FetchAsync(keywords, maxResults, cancellationToken);

    public Task<IReadOnlyList<Opportunity>> FetchWeWorkRemotelyContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default)
        => _weWorkRemotely.FetchAsync(keywords, maxResults, cancellationToken);

    public Task<IReadOnlyList<Opportunity>> FetchRemotiveContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default)
        => _remotive.FetchAsync(keywords, maxResults, cancellationToken);

    public Task<IReadOnlyList<Opportunity>> FetchJobicyContractJobsAsync(string? keywords = null, int maxResults = 15, CancellationToken cancellationToken = default)
        => _jobicy.FetchAsync(keywords, maxResults, cancellationToken);

    public async Task<IReadOnlyList<Opportunity>> FetchAllLiveContractJobsAsync(
        string? keywords = null,
        IEnumerable<string>? sources = null,
        int maxResults = 30,
        CancellationToken cancellationToken = default)
    {
        var combined = new List<Opportunity>();
        var selected = sources?.Select(s => s.ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();

        bool checkAll = selected.Count == 0 || selected.Contains("all") || selected.Contains("approved feed") || selected.Contains("public job board") || selected.Contains("company careers");

        var perSource = Math.Max(5, maxResults / 4);

        // 1. Hacker News
        if (checkAll || selected.Any(s => s.Contains("hacker") || s.Contains("hn")))
        {
            var hn = await FetchHackerNewsContractJobsAsync(keywords, perSource, cancellationToken);
            combined.AddRange(hn);
        }

        // 2. We Work Remotely
        if (checkAll || selected.Any(s => s.Contains("wework") || s.Contains("wwr")))
        {
            var wwr = await FetchWeWorkRemotelyContractJobsAsync(keywords, perSource, cancellationToken);
            combined.AddRange(wwr);
        }

        // 3. Remotive
        if (checkAll || selected.Any(s => s.Contains("remotive")))
        {
            var rmt = await FetchRemotiveContractJobsAsync(keywords, perSource, cancellationToken);
            combined.AddRange(rmt);
        }

        // 4. Jobicy
        if (checkAll || selected.Any(s => s.Contains("jobicy")))
        {
            var jbc = await FetchJobicyContractJobsAsync(keywords, perSource, cancellationToken);
            combined.AddRange(jbc);
        }

        return combined.DistinctBy(o => o.DeduplicationKey).Take(maxResults).ToList();
    }
}
