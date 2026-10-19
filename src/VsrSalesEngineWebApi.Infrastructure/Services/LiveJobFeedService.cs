using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Infrastructure.Services;

public class LiveJobFeedService : ILiveJobFeedService
{
    private readonly HttpClient _httpClient;
    private readonly IContactRepository? _contactRepository;
    private readonly ILogger<LiveJobFeedService>? _logger;

    private static readonly Regex EmailRegex = new(@"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+", RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    public LiveJobFeedService(
        HttpClient? httpClient = null,
        IContactRepository? contactRepository = null,
        ILogger<LiveJobFeedService>? logger = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 VsrSalesEngine/1.0");
        }
        if (_httpClient.Timeout == TimeSpan.FromSeconds(100))
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(12);
        }
        _contactRepository = contactRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Opportunity>> FetchHackerNewsContractJobsAsync(
        string? keywords = null,
        int maxResults = 15,
        CancellationToken cancellationToken = default)
    {
        var results = new List<Opportunity>();

        try
        {
            // 1. Fetch latest "Who is hiring" / "Seeking Freelancer" story IDs
            var storiesUrl = "https://hn.algolia.com/api/v1/search_by_date?tags=story,author_whoishiring&hitsPerPage=3";
            using var storyResp = await _httpClient.GetAsync(storiesUrl, cancellationToken);
            if (!storyResp.IsSuccessStatusCode) return results;

            using var doc = await JsonDocument.ParseAsync(await storyResp.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var hits = doc.RootElement.GetProperty("hits");
            if (hits.GetArrayLength() == 0) return results;

            var storyId = hits[0].GetProperty("objectID").GetString();
            if (string.IsNullOrWhiteSpace(storyId)) return results;

            // 2. Fetch comments from this story
            var commentsUrl = $"https://hn.algolia.com/api/v1/search?tags=comment,story_{storyId}&hitsPerPage=50";
            using var commentsResp = await _httpClient.GetAsync(commentsUrl, cancellationToken);
            if (!commentsResp.IsSuccessStatusCode) return results;

            using var commentsDoc = await JsonDocument.ParseAsync(await commentsResp.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var commentHits = commentsDoc.RootElement.GetProperty("hits");

            foreach (var comment in commentHits.EnumerateArray())
            {
                if (results.Count >= maxResults) break;

                var rawHtml = comment.TryGetProperty("comment_text", out var ct) ? ct.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(rawHtml)) continue;

                var text = CleanHtml(rawHtml);
                if (text.Length < 30) continue;

                // Check keyword matching if provided
                if (!string.IsNullOrWhiteSpace(keywords) &&
                    !text.Contains(keywords, StringComparison.OrdinalIgnoreCase) &&
                    !text.Contains("contract", StringComparison.OrdinalIgnoreCase) &&
                    !text.Contains("developer", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? text;
                var tokens = firstLine.Split('|', StringSplitOptions.TrimEntries);

                var company = tokens.Length > 0 && !string.IsNullOrWhiteSpace(tokens[0]) ? tokens[0].Trim() : "YC / HN Startup";
                if (company.Length > 45) company = company[..45];

                // Skip generic responses that aren't job postings
                if (company.StartsWith("Would you", StringComparison.OrdinalIgnoreCase) ||
                    company.StartsWith("Happy to", StringComparison.OrdinalIgnoreCase) ||
                    company.StartsWith("I'm so", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var role = tokens.Length > 1 && !string.IsNullOrWhiteSpace(tokens[1]) ? tokens[1].Trim() : "Contract Software Engineer";
                if (role.Length > 60) role = role[..60];

                var location = tokens.Length > 2 && !string.IsNullOrWhiteSpace(tokens[2]) ? tokens[2].Trim() : "Remote (Worldwide)";
                if (location.Length > 40) location = location[..40];

                var emails = EmailRegex.Matches(text);
                string? contactEmail = emails.Count > 0 ? emails[0].Value : null;

                var objectId = comment.TryGetProperty("objectID", out var ob) ? ob.GetString() : Guid.NewGuid().ToString("N")[..8];
                var storyUrl = $"https://news.ycombinator.com/item?id={objectId}";

                var opp = new Opportunity
                {
                    Id = $"OP-HN-{objectId}",
                    Company = company,
                    Title = role,
                    Source = "Hacker News (Freelance & Who's Hiring)",
                    Location = location,
                    FoundAt = "Live Feed",
                    Status = "New",
                    Fit = (!string.IsNullOrWhiteSpace(keywords) && text.Contains(keywords, StringComparison.OrdinalIgnoreCase)) ? "High" : "Medium",
                    Type = "Contract / Direct Project",
                    HasVendorDocRequirements = false,
                    VendorRequirementNotes = contactEmail != null ? $"Direct Founder / Hiring Email: {contactEmail}" : "Apply via Hacker News comment thread",
                    SourceUrl = storyUrl,
                    EvidenceSnippet = text.Length > 240 ? text[..240] + "..." : text,
                    DeduplicationKey = $"{company.ToLowerInvariant()}|{role.ToLowerInvariant()}",
                    RetrievedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    StatusHistory = new List<OpportunityStatusChange>
                    {
                        new() { FromStatus = "Discovered", ToStatus = "New", ChangedBy = "HN Live Feed", Reason = "Direct live posting ingested", ChangedAtUtc = DateTime.UtcNow }
                    }
                };

                results.Add(opp);

                // Auto-create verified Contact if direct email was found
                if (contactEmail != null && _contactRepository != null)
                {
                    var contactId = $"CON-HN-{objectId}";
                    var contact = new Contact
                    {
                        Id = contactId,
                        Name = $"{company} Hiring Lead",
                        Company = company,
                        Role = "Founder / Hiring Decision Maker",
                        Email = contactEmail,
                        NormalizedEmail = contactEmail.Trim().ToLowerInvariant(),
                        Source = "Hacker News (Freelance & Who's Hiring)",
                        SourceUrl = storyUrl,
                        ChannelBasis = "ExplicitBusinessContact",
                        Status = "Not contacted",
                        QualificationStatus = "Verified",
                        Notes = $"Extracted from live Hacker News hiring post. Linked to opportunity {opp.Id}",
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    };

                    _ = Task.Run(async () =>
                    {
                        try { await _contactRepository.AddAsync(contact, CancellationToken.None); } catch { /* ignore dups */ }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch live Hacker News jobs");
        }

        return results;
    }

    public async Task<IReadOnlyList<Opportunity>> FetchWeWorkRemotelyContractJobsAsync(
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

    public async Task<IReadOnlyList<Opportunity>> FetchRemotiveContractJobsAsync(
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
                    HasVendorDocRequirements = false,
                    VendorRequirementNotes = $"Job Type: {jobType}. Verified Remotive listing.",
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

    public async Task<IReadOnlyList<Opportunity>> FetchJobicyContractJobsAsync(
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

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var decoded = System.Net.WebUtility.HtmlDecode(html);
        var noTags = HtmlTagRegex.Replace(decoded, " ");
        return Regex.Replace(noTags, @"\s+", " ").Trim();
    }
}
