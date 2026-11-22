using System.Net;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Services.AgentRelay.People;
using Xunit;

namespace VsrSalesEngineWebApi.Tests;

public class PeopleSafetyTests
{
    // ---- SSRF guard -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("198.18.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::7f00:1")]
    public void NonPublicAddressesAreRefused(string address) =>
        Assert.False(SsrfGuard.IsPublicAddress(IPAddress.Parse(address)), address);

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void PublicAddressesAreAllowed(string address) =>
        Assert.True(SsrfGuard.IsPublicAddress(IPAddress.Parse(address)), address);

    [Theory]
    [InlineData("https://acme.io/team", true)]
    [InlineData("http://acme.io/", true)]
    [InlineData("ftp://acme.io/", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("https://user:secret@acme.io/", false)]
    [InlineData("https://acme.io:8443/", false)]
    [InlineData("http://127.0.0.1/", false)]
    [InlineData("http://[::1]/", false)]
    [InlineData("http://10.1.2.3/", false)]
    [InlineData("https://169.254.169.254/latest/meta-data", false)]
    public void OnlyPlainPublicWebAddressesPassTheUriCheck(string url, bool expected) =>
        Assert.Equal(expected, SsrfGuard.IsAllowedUri(new Uri(url)));

    [Fact]
    public async Task HostIsSafeOnlyWhenEveryResolvedAddressIsPublic()
    {
        var resolver = new FakeResolver()
            .Map("public.test", "93.184.216.34")
            .Map("internal.test", "10.0.0.5")
            .Map("mixed.test", "93.184.216.34", "192.168.0.9");

        Assert.True(await SsrfGuard.IsSafeAsync(new Uri("https://public.test/"), resolver));
        Assert.False(await SsrfGuard.IsSafeAsync(new Uri("https://internal.test/"), resolver));
        Assert.False(await SsrfGuard.IsSafeAsync(new Uri("https://mixed.test/"), resolver));
        Assert.False(await SsrfGuard.IsSafeAsync(new Uri("https://unknown.test/"), resolver));
        Assert.False(await SsrfGuard.IsSafeAsync(new Uri("http://169.254.169.254/"), resolver));
    }

    // ---- robots.txt -------------------------------------------------------------------------------------------

    [Fact]
    public void DisallowedPathsAreRefusedAndOthersAllowed()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /team\n", CrawlerIdentity.Token);

        Assert.False(rules.IsAllowed("/team"));
        Assert.False(rules.IsAllowed("/teams/x"));
        Assert.True(rules.IsAllowed("/about"));
    }

    [Fact]
    public void OurOwnGroupBeatsTheWildcardGroup()
    {
        var text = "User-agent: *\nDisallow: /\n\nUser-agent: VsrSalesEngine\nDisallow: /about\n";
        var rules = RobotsRules.Parse(text, CrawlerIdentity.Token);

        Assert.False(rules.IsAllowed("/about"));
        Assert.True(rules.IsAllowed("/team"));
    }

    [Fact]
    public void LongestMatchWinsAndTiesAllow()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /company\nAllow: /company/team\n", CrawlerIdentity.Token);

        Assert.True(rules.IsAllowed("/company/team"));
        Assert.False(rules.IsAllowed("/company/news"));

        var tie = RobotsRules.Parse("User-agent: *\nDisallow: /a\nAllow: /a\n", CrawlerIdentity.Token);
        Assert.True(tie.IsAllowed("/a"));
    }

    [Fact]
    public void WildcardsAndEndAnchorsWork()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /*.pdf$\nDisallow: /private*/\n", CrawlerIdentity.Token);

        Assert.False(rules.IsAllowed("/files/report.pdf"));
        Assert.True(rules.IsAllowed("/files/report.pdf?download=1"));
        Assert.False(rules.IsAllowed("/private-area/"));
    }

    [Fact]
    public void EmptyDisallowCommentsAndMissingGroupsAllowEverything()
    {
        Assert.True(RobotsRules.Parse("User-agent: *\nDisallow:\n", CrawlerIdentity.Token).IsAllowed("/team"));
        Assert.True(RobotsRules.Parse("# nothing here\n", CrawlerIdentity.Token).IsAllowed("/team"));
        Assert.True(RobotsRules.Parse("User-agent: googlebot\nDisallow: /\n", CrawlerIdentity.Token).IsAllowed("/team"));
        Assert.True(RobotsRules.AllowAll.IsAllowed("/anything"));
    }

    [Fact]
    public void SeveralUserAgentLinesShareOneGroup()
    {
        var rules = RobotsRules.Parse("User-agent: otherbot\nUser-agent: VsrSalesEngine\nDisallow: /team\n", CrawlerIdentity.Token);

        Assert.False(rules.IsAllowed("/team"));
    }

    // ---- company site discovery ------------------------------------------------------------------------------------

    private static Opportunity Post(string sourceUrl, string? companyUrl = null, string? text = null) => new()
    {
        Id = "OP-X", Company = "Acme", Title = "Developer", SourceUrl = sourceUrl, CompanyUrl = companyUrl, PostingText = text
    };

    [Theory]
    [InlineData("https://news.ycombinator.com/item?id=1")]
    [InlineData("https://weworkremotely.com/remote-jobs/acme-dev")]
    [InlineData("https://remotive.com/remote-jobs/software-dev/dev-1")]
    [InlineData("https://jobicy.com/jobs/1-dev")]
    [InlineData("https://www.linkedin.com/jobs/view/123")]
    [InlineData("https://in.indeed.com/viewjob?jk=1")]
    [InlineData("https://boards.greenhouse.io/acme/jobs/1")]
    [InlineData("https://jobs.lever.co/acme/1")]
    [InlineData("https://apply.workable.com/acme/j/1")]
    [InlineData("https://jobs.ashbyhq.com/acme")]
    [InlineData("https://www.facebook.com/acme")]
    [InlineData("https://www.instagram.com/acme")]
    [InlineData("https://www.youtube.com/@acme")]
    [InlineData("http://10.0.0.1/careers")]
    [InlineData("https://localhost/careers")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    public void JobBoardsSocialSitesAndNonsenseAreNotCompanyWebsites(string url) =>
        Assert.Null(CompanySiteLocator.FromOpportunity(Post(url)));

    [Fact]
    public void ACompanyLinkBecomesAnHttpsOrigin()
    {
        var site = CompanySiteLocator.FromOpportunity(Post("http://Acme.io/careers/senior-dev?ref=1"));

        Assert.Equal("https://acme.io/", site!.ToString());
    }

    [Fact]
    public void TheFeedsCompanyWebsiteFieldComesFirst()
    {
        var site = CompanySiteLocator.FromOpportunity(Post("https://acme.io/careers/1", companyUrl: "https://www.beta.co/about"));

        Assert.Equal("www.beta.co", site!.Host);
    }

    [Fact]
    public void ALinkInsideThePostTextIsUsedWhenThePostingLinkIsAJobBoard()
    {
        var text = "Apply on https://github.com/acme/jobs or read https://gamma.dev/careers, thanks.";

        var site = CompanySiteLocator.FromOpportunity(Post("https://news.ycombinator.com/item?id=9", text: text));

        Assert.Equal("https://gamma.dev/", site!.ToString());
    }

    // ---- one request a second per host --------------------------------------------------------------------------------

    [Fact]
    public async Task RequestsToTheSameHostAreSpacedOneSecondApart()
    {
        var now = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
        var waits = new List<TimeSpan>();
        var throttle = new HostThrottle(TimeSpan.FromSeconds(1), () => now, (delay, _) => { waits.Add(delay); return Task.CompletedTask; });

        await throttle.WaitAsync("acme.io", default);       // first: no wait
        await throttle.WaitAsync("acme.io", default);       // second, same instant: 1 s
        await throttle.WaitAsync("acme.io", default);       // third: 2 s (second slot is taken)
        await throttle.WaitAsync("other.io", default);      // another host: no wait

        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, waits);

        now = now.AddSeconds(30);
        await throttle.WaitAsync("acme.io", default);       // long enough ago: no wait
        Assert.Equal(2, waits.Count);
    }
}
