using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class SourceRegistryRepository : ISourceRegistryRepository
{
    private static readonly ConcurrentDictionary<string, SourceDefinition> Sources = new(StringComparer.OrdinalIgnoreCase);

    static SourceRegistryRepository()
    {
        var seed = new List<SourceDefinition>
        {
            new()
            {
                Id = "SRC-001",
                Name = "Company careers",
                Description = "Target corporate career pages and approved public job boards.",
                Category = "Careers",
                PolicyStatus = "Active",
                AllowsDirectFetch = true,
                TermsUrl = "https://vsrsystems.com/compliance/source-policy",
                AttributionNotice = "Extracted from public career portal with source link retained.",
                DailyRequestLimit = 500,
                CurrentDayRequests = 42,
                HealthStatus = "Healthy",
                LastCheckedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = "SRC-002",
                Name = "Public job board",
                Description = "Public indexing boards and technology work listings.",
                Category = "JobBoard",
                PolicyStatus = "Active",
                AllowsDirectFetch = true,
                TermsUrl = "https://vsrsystems.com/compliance/source-policy",
                AttributionNotice = "Referenced from public job board listing.",
                DailyRequestLimit = 300,
                CurrentDayRequests = 18,
                HealthStatus = "Healthy",
                LastCheckedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = "SRC-003",
                Name = "Direct company research",
                Description = "Verified manual and automated deep research on target organizations.",
                Category = "Research",
                PolicyStatus = "Active",
                AllowsDirectFetch = true,
                TermsUrl = "https://vsrsystems.com/compliance/source-policy",
                AttributionNotice = "Verified research entry by sales team member.",
                DailyRequestLimit = 200,
                CurrentDayRequests = 9,
                HealthStatus = "Healthy",
                LastCheckedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = "SRC-004",
                Name = "Public procurement",
                Description = "Government and public sector RFP / vendor registration portals.",
                Category = "Procurement",
                PolicyStatus = "Active",
                AllowsDirectFetch = true,
                TermsUrl = "https://vsrsystems.com/compliance/source-policy",
                AttributionNotice = "Public procurement notice / tender reference.",
                DailyRequestLimit = 150,
                CurrentDayRequests = 3,
                HealthStatus = "Healthy",
                LastCheckedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = "SRC-005",
                Name = "Approved feed",
                Description = "Syndicated developer contract and technology requirement feeds.",
                Category = "Feed",
                PolicyStatus = "Active",
                AllowsDirectFetch = true,
                TermsUrl = "https://vsrsystems.com/compliance/source-policy",
                AttributionNotice = "Syndicated feed item with canonical URL.",
                DailyRequestLimit = 400,
                CurrentDayRequests = 24,
                HealthStatus = "Healthy",
                LastCheckedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = "SRC-006",
                Name = "LinkedIn handoff",
                Description = "Manual browser handoff only. Automated scraping and automated DMs prohibited by policy.",
                Category = "Social",
                PolicyStatus = "Restricted",
                AllowsDirectFetch = false,
                TermsUrl = "https://vsrsystems.com/compliance/source-policy",
                AttributionNotice = "Manual external browser link only. No automated access.",
                DailyRequestLimit = 0,
                CurrentDayRequests = 0,
                HealthStatus = "Healthy",
                LastCheckedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = "SRC-007",
                Name = "Hacker News (Freelance & Who's Hiring)",
                Description = "Live founder and engineering lead contractor postings from monthly Hacker News threads.",
                Category = "ContractBoard",
                PolicyStatus = "Active",
                AllowsDirectFetch = true,
                TermsUrl = "https://news.ycombinator.com",
                AttributionNotice = "Public Hacker News community thread with canonical story link.",
                DailyRequestLimit = 600,
                CurrentDayRequests = 12,
                HealthStatus = "Healthy",
                LastCheckedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = "SRC-008",
                Name = "We Work Remotely (Contracts)",
                Description = "Live verified remote programming and software contract feeds from We Work Remotely.",
                Category = "ContractBoard",
                PolicyStatus = "Active",
                AllowsDirectFetch = true,
                TermsUrl = "https://weworkremotely.com",
                AttributionNotice = "Syndicated WWR RSS feed with canonical job URL.",
                DailyRequestLimit = 500,
                CurrentDayRequests = 15,
                HealthStatus = "Healthy",
                LastCheckedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = "SRC-009",
                Name = "Remotive (Contract / Freelance)",
                Description = "Live contract-only software development projects and developer contractor roles from Remotive.",
                Category = "ContractBoard",
                PolicyStatus = "Active",
                AllowsDirectFetch = true,
                TermsUrl = "https://remotive.com",
                AttributionNotice = "Remotive public API with canonical job application URL.",
                DailyRequestLimit = 500,
                CurrentDayRequests = 8,
                HealthStatus = "Healthy",
                LastCheckedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = "SRC-010",
                Name = "Jobicy (Developer Contracts)",
                Description = "Live remote engineering contracts and developer projects from Jobicy.",
                Category = "ContractBoard",
                PolicyStatus = "Active",
                AllowsDirectFetch = true,
                TermsUrl = "https://jobicy.com",
                AttributionNotice = "Jobicy Engineering API with direct job listing link.",
                DailyRequestLimit = 400,
                CurrentDayRequests = 5,
                HealthStatus = "Healthy",
                LastCheckedAtUtc = DateTime.UtcNow
            }
        };

        foreach (var s in seed)
        {
            Sources[s.Id] = s;
        }
    }

    public Task<IReadOnlyList<SourceDefinition>> GetAllSourcesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SourceDefinition> list = Sources.Values.OrderBy(s => s.Id).ToList();
        return Task.FromResult(list);
    }

    public Task<SourceDefinition?> GetSourceByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        Sources.TryGetValue(id, out var src);
        return Task.FromResult(src);
    }

    public Task<SourceDefinition?> UpdatePolicyStatusAsync(string id, string status, CancellationToken cancellationToken = default)
    {
        if (Sources.TryGetValue(id, out var src))
        {
            src.PolicyStatus = status;
            src.LastCheckedAtUtc = DateTime.UtcNow;
            return Task.FromResult<SourceDefinition?>(src);
        }
        return Task.FromResult<SourceDefinition?>(null);
    }

    public Task IncrementDailyRequestCountAsync(string id, int count = 1, CancellationToken cancellationToken = default)
    {
        if (Sources.TryGetValue(id, out var src))
        {
            src.CurrentDayRequests += count;
        }
        return Task.CompletedTask;
    }
}
