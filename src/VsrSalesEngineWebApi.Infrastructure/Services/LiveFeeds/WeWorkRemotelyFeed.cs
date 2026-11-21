using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using static VsrSalesEngineWebApi.Infrastructure.Services.LiveFeeds.FeedText;

namespace VsrSalesEngineWebApi.Infrastructure.Services.LiveFeeds;

internal sealed class WeWorkRemotelyFeed
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;

    public WeWorkRemotelyFeed(HttpClient httpClient, ILogger? logger)
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
            var feedUrls = new[]
            {
                "https://weworkremotely.com/categories/remote-programming-jobs.rss",
                "https://weworkremotely.com/categories/remote-full-stack-programming-jobs.rss",
                "https://weworkremotely.com/categories/remote-back-end-programming-jobs.rss"
            };

            foreach (var feedUrl in feedUrls)
            {
                if (results.Count >= maxResults) break;

                using var resp = await _httpClient.GetAsync(feedUrl, cancellationToken);
                if (!resp.IsSuccessStatusCode) continue;

                var xmlContent = await resp.Content.ReadAsStringAsync(cancellationToken);
                var xDoc = XDocument.Parse(xmlContent);

                var items = xDoc.Descendants("item");
                foreach (var item in items)
                {
                    if (results.Count >= maxResults) break;

                    var rawTitle = item.Element("title")?.Value ?? "";
                    var link = item.Element("link")?.Value ?? "";
                    var desc = CleanHtml(item.Element("description")?.Value ?? "");
                    var pubDate = item.Element("pubDate")?.Value ?? "Recently";

                    if (string.IsNullOrWhiteSpace(rawTitle)) continue;

                    // Parse "Company: Job Title"
                    string company = "Remote Tech Co";
                    string title = rawTitle;
                    if (rawTitle.Contains(':'))
                    {
                        var parts = rawTitle.Split(':', 2, StringSplitOptions.TrimEntries);
                        company = parts[0];
                        title = parts[1];
                    }

                    // Check for contract / freelance priority
                    bool isContract = desc.Contains("contract", StringComparison.OrdinalIgnoreCase) ||
                                      desc.Contains("freelance", StringComparison.OrdinalIgnoreCase) ||
                                      title.Contains("contract", StringComparison.OrdinalIgnoreCase) ||
                                      title.Contains("freelance", StringComparison.OrdinalIgnoreCase);

                    if (!string.IsNullOrWhiteSpace(keywords) &&
                        !title.Contains(keywords, StringComparison.OrdinalIgnoreCase) &&
                        !desc.Contains(keywords, StringComparison.OrdinalIgnoreCase) &&
                        !isContract)
                    {
                        continue;
                    }

                    var opp = new Opportunity
                    {
                        Id = $"OP-WWR-{Math.Abs(link.GetHashCode()):D6}",
                        Company = company,
                        Title = title,
                        Source = "We Work Remotely (Contracts)",
                        Location = "Worldwide Remote",
                        FoundAt = pubDate.Length > 16 ? pubDate[..16] : pubDate,
                        Status = "New",
                        Fit = isContract ? "High" : "Medium",
                        Type = isContract ? "Contract" : "Full-Time / Contract",
                        HasVendorDocRequirements = false,
                        VendorRequirementNotes = "Verified listing on We Work Remotely platform",
                        SourceUrl = link,
                        EvidenceSnippet = desc.Length > 240 ? desc[..240] + "..." : desc,
                        DeduplicationKey = $"{company.ToLowerInvariant()}|{title.ToLowerInvariant()}",
                        RetrievedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow,
                        StatusHistory = new List<OpportunityStatusChange>
                        {
                            new() { FromStatus = "Discovered", ToStatus = "New", ChangedBy = "WWR RSS Feed", Reason = "Live feed item indexed", ChangedAtUtc = DateTime.UtcNow }
                        }
                    };

                    results.Add(opp);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch live We Work Remotely jobs");
        }

        return results;
    }
}
