using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using static VsrSalesEngineWebApi.Infrastructure.Services.LiveFeeds.FeedText;

namespace VsrSalesEngineWebApi.Infrastructure.Services.LiveFeeds;

internal sealed class RemotiveFeed
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;

    public RemotiveFeed(HttpClient httpClient, ILogger? logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Opportunity>> FetchAsync(
        string? keywords = null,
        int maxResults = 15,
        CancellationToken cancellationToken = default)
    {
        var results = new List<Opportunity>();

        try
        {
            var url = "https://remotive.com/api/remote-jobs?category=software-dev&limit=40";
            using var resp = await _httpClient.GetAsync(url, cancellationToken);
            if (!resp.IsSuccessStatusCode) return results;

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var jobs = doc.RootElement.GetProperty("jobs");

            foreach (var job in jobs.EnumerateArray())
            {
                if (results.Count >= maxResults) break;

                var company = job.TryGetProperty("company_name", out var c) ? c.GetString() ?? "" : "";
                var title = job.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var jobType = job.TryGetProperty("job_type", out var jt) ? jt.GetString() ?? "" : "";
                var jobUrl = job.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                var rawDesc = job.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                var location = job.TryGetProperty("candidate_required_location", out var loc) ? loc.GetString() ?? "Worldwide" : "Worldwide";

                if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(title)) continue;

                bool isContract = jobType.Contains("contract", StringComparison.OrdinalIgnoreCase) ||
                                  jobType.Contains("freelance", StringComparison.OrdinalIgnoreCase);

                var cleanDesc = CleanHtml(rawDesc);

                if (!string.IsNullOrWhiteSpace(keywords) &&
                    !title.Contains(keywords, StringComparison.OrdinalIgnoreCase) &&
                    !cleanDesc.Contains(keywords, StringComparison.OrdinalIgnoreCase) &&
                    !isContract)
                {
                    continue;
                }

                var opp = new Opportunity
                {
                    Id = $"OP-RMT-{Math.Abs(jobUrl.GetHashCode()):D6}",
                    Company = company,
                    Title = title,
                    Source = "Remotive (Contract / Freelance)",
                    Location = string.IsNullOrWhiteSpace(location) ? "Remote" : location,
                    FoundAt = "Live Feed",
                    Status = "New",
                    Fit = isContract ? "High" : "Medium",
                    Type = isContract ? "Contract" : "Direct project",
                    SourceUrl = jobUrl,
                    EvidenceSnippet = cleanDesc.Length > 240 ? cleanDesc[..240] + "..." : cleanDesc,
                    DeduplicationKey = $"{company.ToLowerInvariant()}|{title.ToLowerInvariant()}",
                    RetrievedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    StatusHistory = new List<OpportunityStatusChange>
                    {
                        new() { FromStatus = "Discovered", ToStatus = "New", ChangedBy = "Remotive Live API", Reason = "Live API feed item indexed", ChangedAtUtc = DateTime.UtcNow }
                    }
                };

                results.Add(opp);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch live Remotive jobs");
        }

        return results;
    }
}
