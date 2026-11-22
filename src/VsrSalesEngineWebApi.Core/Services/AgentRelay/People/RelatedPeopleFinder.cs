using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>
/// Finds people connected to a post. Network use is limited to the company's own public pages: at most four
/// pages per company, robots.txt respected, one request a second per host, never a social network.
/// </summary>
public sealed class RelatedPeopleFinder : IRelatedPeopleFinder
{
    public const int MaxPages = 4;
    public const int MaxPeople = 5;

    private static readonly string[] Paths = { "/about", "/team", "/company", "/leadership", "/about-us", "/careers", "/jobs" };

    private sealed record SiteScan(SiteOutcome Outcome, IReadOnlyList<RelatedPerson> People, int PagesRead, string? Detail);

    private readonly IPublicPageFetcher _fetcher;
    private readonly Dictionary<string, RobotsRules?> _robots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SiteScan> _sites = new(StringComparer.OrdinalIgnoreCase);

    public RelatedPeopleFinder(IPublicPageFetcher fetcher)
    {
        _fetcher = fetcher;
    }

    public async Task<RelatedPeopleResult> FindAsync(Opportunity opportunity, bool allowNetwork, CancellationToken cancellationToken = default)
    {
        var fromPost = PostingPeopleExtractor.Extract(opportunity);
        var site = CompanySiteLocator.FromOpportunity(opportunity);
        if (site == null) return Build(fromPost, SiteOutcome.NoCompanySite, null, 0, null, opportunity);

        if (!_sites.TryGetValue(site.Host, out var scan))
        {
            if (!allowNetwork) return Build(fromPost, SiteOutcome.NotChecked, site.Host, 0, null, opportunity);
            scan = await ScanAsync(site, cancellationToken);
            _sites[site.Host] = scan;
        }

        return Build(fromPost.Concat(scan.People).ToList(), scan.Outcome, site.Host, scan.PagesRead, scan.Detail, opportunity);
    }

    private async Task<SiteScan> ScanAsync(Uri site, CancellationToken ct)
    {
        var robots = await GetRobotsAsync(site, ct);
        if (robots == null) return new SiteScan(SiteOutcome.RobotsDisallowed, Array.Empty<RelatedPerson>(), 0, "robots.txt could not be read");

        var allowed = Paths.Where(robots.IsAllowed).ToList();
        if (allowed.Count == 0) return new SiteScan(SiteOutcome.RobotsDisallowed, Array.Empty<RelatedPerson>(), 0, null);

        var people = new List<RelatedPerson>();
        var pages = 0;
        var failed = false;

        foreach (var path in allowed)
        {
            if (pages >= MaxPages) break;

            var page = await _fetcher.GetAsync(new Uri(site, path), ct);
            if (page.Error != null) { failed = true; continue; }
            if (!page.IsHtml || !robots.IsAllowed(page.FinalUri.PathAndQuery)) continue;

            pages++;
            people.AddRange(PagePeopleExtractor.Extract(HtmlTextLines.Extract(page.Body), page.FinalUri.ToString()));
        }

        var outcome = people.Count > 0 ? SiteOutcome.Found
            : pages > 0 ? SiteOutcome.NoPeopleFound
            : failed ? SiteOutcome.FetchFailed
            : SiteOutcome.NoPages;
        return new SiteScan(outcome, people, pages, null);
    }

    /// <summary>Null means robots.txt could not be read, so the site is left alone.</summary>
    private async Task<RobotsRules?> GetRobotsAsync(Uri site, CancellationToken ct)
    {
        if (_robots.TryGetValue(site.Host, out var cached)) return cached;

        var page = await _fetcher.GetAsync(new Uri(site, "/robots.txt"), ct);
        RobotsRules? rules;
        if (page.Error != null || page.StatusCode == 429 || page.StatusCode >= 500) rules = null;
        else if (page.StatusCode == 200) rules = RobotsRules.Parse(page.Body, CrawlerIdentity.Token);
        else rules = RobotsRules.AllowAll;

        _robots[site.Host] = rules;
        return rules;
    }

    private static RelatedPeopleResult Build(IReadOnlyList<RelatedPerson> found, SiteOutcome outcome, string? host, int pages, string? detail, Opportunity opportunity)
    {
        var company = PersonNames.Key(opportunity.Company);

        var ranked = found
            .Select((person, index) => (person, index))
            .GroupBy(x => PersonNames.Key(x.person.Handle ?? x.person.Name) + "|" + company)
            .Select(g => g.OrderBy(x => PersonRelation.Rank(x.person.Relation)).ThenBy(x => x.person.Confidence == "high" ? 0 : 1).ThenBy(x => x.index).First())
            .OrderBy(x => PersonRelation.Rank(x.person.Relation))
            .ThenBy(x => x.person.Confidence == "high" ? 0 : 1)
            .ThenBy(x => x.index)
            .Select(x => x.person)
            .Take(MaxPeople)
            .ToList();

        return new RelatedPeopleResult(ranked, outcome, host, pages, detail);
    }
}
