using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;
using VsrSalesEngineWebApi.Core.Services.AgentRelay.People;
using static VsrSalesEngineWebApi.Infrastructure.Services.LiveFeeds.FeedText;

namespace VsrSalesEngineWebApi.Infrastructure.Services.LiveFeeds;

internal sealed class HackerNewsFeed
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;
    private readonly IContactRepository? _contactRepository;

    public HackerNewsFeed(HttpClient httpClient, ILogger? logger, IContactRepository? contactRepository)
    {
        _httpClient = httpClient;
        _logger = logger;
        _contactRepository = contactRepository;
    }

    public async Task<IReadOnlyList<Opportunity>> FetchAsync(
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
                var author = comment.TryGetProperty("author", out var authorElement) ? authorElement.GetString() : null;
                var posterHandle = string.IsNullOrWhiteSpace(author) ? null : author.Trim();

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
                    SourceUrl = storyUrl,
                    EvidenceSnippet = text.Length > 240 ? text[..240] + "..." : text,
                    PosterHandle = posterHandle,
                    PosterProfileUrl = posterHandle == null ? null : $"https://news.ycombinator.com/user?id={Uri.EscapeDataString(posterHandle)}",
                    PostingText = text.Length > 4000 ? text[..4000] : text,
                    CompanyUrl = CompanySiteLocator.FromText(text)?.ToString(),
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
                        RelationToPost = "poster",
                        Notes = $"Extracted from live Hacker News hiring post. Linked to opportunity {opp.Id}",
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    };

                    try
                    {
                        var existingContact = await _contactRepository.GetByIdAsync(contactId, cancellationToken);
                        if (existingContact == null)
                        {
                            await _contactRepository.AddAsync(contact, cancellationToken);
                        }
                    }
                    catch (Exception contactEx)
                    {
                        _logger?.LogWarning(contactEx, "Failed to auto-create contact for live Hacker News posting {ContactId}", contactId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch live Hacker News jobs");
        }

        return results;
    }
}
