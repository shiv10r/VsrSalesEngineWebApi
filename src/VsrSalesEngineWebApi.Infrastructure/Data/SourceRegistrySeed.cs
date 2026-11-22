using VsrSalesEngineWebApi.Core.Entities;

namespace VsrSalesEngineWebApi.Infrastructure.Data;

/// <summary>
/// The source registry. Only sources that have a real reader (the four live job feeds) or a clear policy
/// note (LinkedIn, manual only) are listed. Counters start at 0 and health at Unknown until a real run
/// records otherwise. The live feeds are switched off by default; the owner can enable them in Settings.
/// </summary>
internal static class SourceRegistrySeed
{
    private const string Policy = "https://vsrsystems.com/compliance/source-policy";

    public static IReadOnlyList<SourceDefinition> All() => new[]
    {
        New("SRC-006", "LinkedIn handoff", "Manual browser handoff only. Automated scraping and automated DMs prohibited by policy.", "Social", "Restricted", false, Policy, "Manual external browser link only. No automated access.", 0),
        New("SRC-007", "Hacker News (Freelance & Who's Hiring)", "Remote contractor postings from monthly Hacker News threads. Mostly foreign remote work.", "ContractBoard", "Disabled", true, "https://news.ycombinator.com", "Public Hacker News community thread with canonical story link.", 600),
        New("SRC-008", "We Work Remotely (Contracts)", "Remote programming contract feed from We Work Remotely. Mostly foreign remote work.", "ContractBoard", "Disabled", true, "https://weworkremotely.com", "Syndicated WWR RSS feed with canonical job URL.", 500),
        New("SRC-009", "Remotive (Contract / Freelance)", "Remote contract software projects from Remotive. Mostly foreign remote work.", "ContractBoard", "Disabled", true, "https://remotive.com", "Remotive public API with canonical job application URL.", 500),
        New("SRC-010", "Jobicy (Developer Contracts)", "Remote engineering contracts from Jobicy. Mostly foreign remote work.", "ContractBoard", "Disabled", true, "https://jobicy.com", "Jobicy Engineering API with direct job listing link.", 400)
    };

    private static SourceDefinition New(
        string id, string name, string description, string category, string policyStatus,
        bool allowsDirectFetch, string termsUrl, string attribution, int dailyLimit) => new()
    {
        Id = id, Name = name, Description = description, Category = category, PolicyStatus = policyStatus,
        AllowsDirectFetch = allowsDirectFetch, TermsUrl = termsUrl, AttributionNotice = attribution,
        DailyRequestLimit = dailyLimit, CurrentDayRequests = 0, HealthStatus = "Unknown"
    };
}
