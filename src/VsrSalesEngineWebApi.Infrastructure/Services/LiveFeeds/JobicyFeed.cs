using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using static VsrSalesEngineWebApi.Infrastructure.Services.LiveFeeds.FeedText;

namespace VsrSalesEngineWebApi.Infrastructure.Services.LiveFeeds;

internal sealed class JobicyFeed
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;

    public JobicyFeed(HttpClient httpClient, ILogger? logger)
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
            var url = "https://jobicy.com/api/v2/remote-jobs?count=30&industry=engineering";
            using var resp = await _httpClient.GetAsync(url, cancellationToken);
            if (!resp.IsSuccessStatusCode) return results;

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("jobs", out var jobs)) return results;

            foreach (var job in jobs.EnumerateArray())
            {
                if (results.Count >= maxResults) break;

                var company = job.TryGetProperty("companyName", out var c) ? c.GetString() ?? "" : "";
                var title = job.TryGetProperty("jobTitle", out var t) ? t.GetString() ?? "" : "";
                var jobUrl = job.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                var rawDesc = job.TryGetProperty("jobDescription", out var d) ? d.GetString() ?? "" : "";
                var location = job.TryGetProperty("jobGeo", out var loc) ? loc.GetString() ?? "Worldwide" : "Worldwide";

                if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(title)) continue;

                var cleanDesc = CleanHtml(rawDesc);

                bool isContract = false;
                if (job.TryGetProperty("jobType", out var jtArray) && jtArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in jtArray.EnumerateArray())
                    {
                        var str = item.GetString() ?? "";
                        if (str.Contains("contract", StringComparison.OrdinalIgnoreCase) || str.Contains("freelance", StringComparison.OrdinalIgnoreCase))
                        {
                            isContract = true;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(keywords) &&
                    !title.Contains(keywords, StringComparison.OrdinalIgnoreCase) &&
                    !cleanDesc.Contains(keywords, StringComparison.OrdinalIgnoreCase) &&
                    !isContract)
                {
                    continue;
                }

                var opp = new Opportunity
                {
                    Id = $"OP-JBC-{Math.Abs(jobUrl.GetHashCode()):D6}",
                    Company = company,
                    Title = title,
                    Source = "Jobicy (Developer Contracts)",
                    Location = string.IsNullOrWhiteSpace(location) ? "Remote" : location,
                    FoundAt = "Live Feed",
                    Status = "New",
                    Fit = isContract ? "High" : "Medium",
                    Type = isContract ? "Contract" : "Direct project",
                    HasVendorDocRequirements = false,
                    VendorRequirementNotes = "Verified listing via Jobicy Engineering API",
                    SourceUrl = jobUrl,
                    EvidenceSnippet = cleanDesc.Length > 240 ? cleanDesc[..240] + "..." : cleanDesc,
                    DeduplicationKey = $"{company.ToLowerInvariant()}|{title.ToLowerInvariant()}",
                    RetrievedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    StatusHistory = new List<OpportunityStatusChange>
                    {
                        new() { FromStatus = "Discovered", ToStatus = "New", ChangedBy = "Jobicy Live API", Reason = "Live API feed item indexed", ChangedAtUtc = DateTime.UtcNow }
                    }
                };

                results.Add(opp);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch live Jobicy jobs");
        }

        return results;
    }
}
