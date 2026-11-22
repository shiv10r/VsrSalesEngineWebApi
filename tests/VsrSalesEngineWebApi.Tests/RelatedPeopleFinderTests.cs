using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Services.AgentRelay.People;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class RelatedPeopleFinderTests
{
    private const string Robots = "https://acme.io/robots.txt";

    private readonly FakePageFetcher _web = new();
    private readonly RelatedPeopleFinder _finder;

    public RelatedPeopleFinderTests() => _finder = new RelatedPeopleFinder(_web);

    private static Opportunity Post(string sourceUrl = "https://acme.io/careers/dev", string? handle = null, string? text = null) => new()
    {
        Id = "OP-1", Company = "Acme", Title = "Senior .NET Developer", Source = "Hacker News (Freelance & Who's Hiring)",
        SourceUrl = sourceUrl, PosterHandle = handle, PostingText = text
    };

    private static string Page(params string[] lines) => "<body>" + string.Concat(lines.Select(l => $"<p>{l}</p>")) + "</body>";

    // ---- where we are allowed to look -------------------------------------------------------------------------------

    [Fact]
    public async Task AJobBoardLinkMeansNoCompanySiteAndNothingIsFetched()
    {
        var result = await _finder.FindAsync(Post("https://news.ycombinator.com/item?id=1", handle: "jsmith"), true);

        Assert.Equal(SiteOutcome.NoCompanySite, result.Site);
        Assert.Empty(_web.Requests);
        Assert.Equal("jsmith", result.People.Single().Name);                 // poster comes from data we already hold
    }

    [Fact]
    public async Task WithNetworkDisallowedOnlyTheDataWeAlreadyHoldIsUsed()
    {
        var result = await _finder.FindAsync(Post(handle: "jsmith"), allowNetwork: false);

        Assert.Equal(SiteOutcome.NotChecked, result.Site);
        Assert.Empty(_web.Requests);
        Assert.Equal(PersonRelation.Poster, result.People.Single().Relation);
    }

    [Fact]
    public async Task RobotsTxtThatBlocksOurTeamPagesIsHonoured()
    {
        _web.Text(Robots, "User-agent: *\nDisallow: /team\nDisallow: /about\n")
            .Html("https://acme.io/team", Page("Rahul Verma, CEO"))
            .Html("https://acme.io/company", Page("Meera Jain, Technical Recruiter"));

        var result = await _finder.FindAsync(Post(), true);

        Assert.DoesNotContain("https://acme.io/team", _web.Requests);
        Assert.DoesNotContain("https://acme.io/about", _web.Requests);
        Assert.DoesNotContain("https://acme.io/about-us", _web.Requests);    // "/about" also blocks "/about-us"
        Assert.Contains("https://acme.io/company", _web.Requests);
        Assert.Equal("Meera Jain", result.People.Single().Name);
    }

    [Fact]
    public async Task RobotsTxtThatBlocksEverythingMeansNothingBeyondRobotsIsFetched()
    {
        _web.Text(Robots, "User-agent: *\nDisallow: /\n");

        var result = await _finder.FindAsync(Post(), true);

        Assert.Equal(SiteOutcome.RobotsDisallowed, result.Site);
        Assert.Null(result.Detail);
        Assert.Equal(new[] { Robots }, _web.Requests);
    }

    [Fact]
    public async Task ARulesGroupAddressedToUsIsHonoured()
    {
        _web.Text(Robots, "User-agent: VsrSalesEngine\nDisallow: /\n");

        var result = await _finder.FindAsync(Post(), true);

        Assert.Equal(SiteOutcome.RobotsDisallowed, result.Site);
    }

    [Fact]
    public async Task AMissingRobotsTxtMeansTheSiteIsOpen()
    {
        _web.Html("https://acme.io/team", Page("Rahul Verma, CEO"));       // robots.txt is a 404

        var result = await _finder.FindAsync(Post(), true);

        Assert.Equal(SiteOutcome.Found, result.Site);
        Assert.Equal("Rahul Verma", result.People.Single().Name);
    }

    [Fact]
    public async Task ARobotsTxtThatCannotBeReadMeansWeLeaveTheSiteAlone()
    {
        _web.Status(Robots, 503);

        var result = await _finder.FindAsync(Post(), true);

        Assert.Equal(SiteOutcome.RobotsDisallowed, result.Site);
        Assert.NotNull(result.Detail);
        Assert.Equal(new[] { Robots }, _web.Requests);
    }

    [Fact]
    public async Task RobotsTxtIsReadOncePerHost()
    {
        var other = Post("https://acme.io/careers/other");
        other.Id = "OP-2";

        await _finder.FindAsync(Post(), true);
        await _finder.FindAsync(other, true);

        Assert.Equal(1, _web.RobotsRequests);
        Assert.Equal(_web.PageRequests.Count(), _web.PageRequests.Distinct().Count());    // pages not re-read either
    }

    // ---- limits -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task AtMostFourPagesAreReadPerCompany()
    {
        foreach (var path in new[] { "about", "team", "company", "leadership", "about-us", "careers", "jobs" })
            _web.Html($"https://acme.io/{path}", Page("Welcome to Acme"));

        var result = await _finder.FindAsync(Post(), true);

        Assert.Equal(4, result.PagesRead);
        Assert.Equal(4, _web.PageRequests.Count());
        Assert.Equal(SiteOutcome.NoPeopleFound, result.Site);
    }

    [Fact]
    public async Task CareersAndJobsPagesAreCheckedToo()
    {
        _web.Html("https://acme.io/careers", Page("Meera Jain, Technical Recruiter"))
            .Html("https://acme.io/jobs", Page("Amit Shah, Tech Lead"));

        var result = await _finder.FindAsync(Post(), true);

        Assert.Contains(result.People, p => p.Name == "Meera Jain" && p.Relation == PersonRelation.Hiring);
        Assert.Contains(result.People, p => p.Name == "Amit Shah" && p.Relation == PersonRelation.Engineering);
    }

    [Fact]
    public async Task AtMostFivePeopleAreKeptBestRankedFirst()
    {
        _web.Html("https://acme.io/team", Page(
            "Rahul Verma, CEO", "Meera Jain, Technical Recruiter", "Ravi Kumar, Engineering Manager", "Amit Shah, Tech Lead"));
        var text = "Acme | Senior .NET Developer | Remote\nContact Jane Roy for details.";

        var result = await _finder.FindAsync(Post(handle: "jsmith", text: text), true);

        Assert.Equal(5, result.People.Count);
        Assert.Equal(
            new[] { PersonRelation.Poster, PersonRelation.Hiring, PersonRelation.DecisionMaker, PersonRelation.Engineering, PersonRelation.Engineering },
            result.People.Select(p => p.Relation).ToArray());
        Assert.DoesNotContain(result.People, p => p.Name == "Jane Roy");     // the lowest-ranked one is the one dropped
    }

    [Fact]
    public async Task APersonOnSeveralPagesOrNamedTwiceIsListedOnceUnderTheirBestRelation()
    {
        _web.Html("https://acme.io/about", Page("Rahul Verma, CEO"))
            .Html("https://acme.io/team", Page("Rahul Verma - Co-founder"))
            .Html("https://acme.io/careers", Page("Meera Jain, Technical Recruiter"));
        var text = "Contact Meera Jain for details.\nPlease ask for Rahul Verma.";

        var result = await _finder.FindAsync(Post(text: text), true);

        Assert.Equal(2, result.People.Count);
        Assert.Equal(PersonRelation.Hiring, result.People.Single(p => p.Name == "Meera Jain").Relation);          // not 'mentioned'
        Assert.Equal(PersonRelation.DecisionMaker, result.People.Single(p => p.Name == "Rahul Verma").Relation);
    }

    // ---- outcomes ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task NoTeamOrCareersPagesIsReportedAsSuch()
    {
        var result = await _finder.FindAsync(Post(), true);

        Assert.Equal(SiteOutcome.NoPages, result.Site);
        Assert.Equal("acme.io", result.Host);
        Assert.Empty(result.People);
    }

    [Fact]
    public async Task PagesThatCannotBeLoadedAreReportedAsAFetchFailure()
    {
        foreach (var path in new[] { "about", "team", "company", "leadership", "about-us", "careers", "jobs" })
            _web.Fail($"https://acme.io/{path}", "timeout");

        var result = await _finder.FindAsync(Post(), true);

        Assert.Equal(SiteOutcome.FetchFailed, result.Site);
    }

    [Fact]
    public async Task NoEmailIsEverGuessedOrBuilt()
    {
        _web.Html("https://acme.io/team", "<p>Rahul Verma, CEO</p><a href=\"mailto:info@acme.io\">info@acme.io</a>");

        var result = await _finder.FindAsync(Post(), true);

        var rahul = result.People.Single();
        Assert.Null(rahul.Email);
        Assert.DoesNotContain(_web.Requests, r => r.Contains('@'));
    }

    [Fact]
    public async Task ADecisionMakerWithAVisibleNamedMailtoKeepsIt()
    {
        _web.Html("https://acme.io/team", "<div><h3><a href=\"mailto:rahul@acme.io\">Rahul Verma</a></h3><p>CEO</p></div>");

        var result = await _finder.FindAsync(Post(), true);

        Assert.Equal("rahul@acme.io", result.People.Single().Email);
    }
}
