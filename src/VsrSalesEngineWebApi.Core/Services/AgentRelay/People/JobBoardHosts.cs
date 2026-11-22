namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>Hosts that are job boards, aggregators, social sites or link shorteners - never a company's own website.</summary>
public static class JobBoardHosts
{
    private static readonly string[] Suffixes =
    {
        "ycombinator.com", "hackernews.com", "weworkremotely.com", "remotive.com", "remotive.io", "jobicy.com",
        "linkedin.com", "lnkd.in", "indeed.com", "glassdoor.com", "greenhouse.io", "lever.co", "workable.com",
        "ashbyhq.com", "smartrecruiters.com", "bamboohr.com", "recruitee.com", "breezy.hr", "myworkdayjobs.com",
        "workday.com", "jobvite.com", "icims.com", "teamtailor.com", "personio.de", "personio.com", "wellfound.com",
        "angel.co", "naukri.com", "monster.com", "ziprecruiter.com", "upwork.com", "freelancer.com", "fiverr.com",
        "toptal.com", "github.com", "gitlab.com", "medium.com", "notion.site", "notion.so", "twitter.com", "x.com",
        "facebook.com", "instagram.com", "youtube.com", "youtu.be", "reddit.com", "google.com", "forms.gle",
        "bit.ly", "tinyurl.com", "t.co", "wikipedia.org", "crunchbase.com"
    };

    public static bool IsJobBoardOrAggregator(string host)
    {
        var h = host.Trim().TrimEnd('.').ToLowerInvariant();
        return Suffixes.Any(s => h == s || h.EndsWith("." + s, StringComparison.Ordinal));
    }
}
