using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class SearchProfileRepository : ISearchProfileRepository
{
    private static readonly ConcurrentDictionary<string, SearchProfile> Profiles = new(StringComparer.OrdinalIgnoreCase);

    static SearchProfileRepository()
    {
        var seed = new List<SearchProfile>
        {
            new()
            {
                Id = "PRF-001",
                Name = ".NET & Cloud Modernization Leads",
                TargetKeywords = ".NET Core, C#, Azure, Microservices, API Modernization",
                ExcludedKeywords = "internship, unpaid, wordpress",
                Location = "India · Remote",
                OpportunityType = "All relevant types",
                PublishedWithinDays = 7,
                SelectedSources = new List<string> { "Company careers", "Public job board", "Approved feed" },
                MaxResults = 50,
                IsActive = true,
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = "PRF-002",
                Name = "React & Full-Stack Frontend Contracts",
                TargetKeywords = "React, TypeScript, Next.js, Frontend Architecture",
                ExcludedKeywords = "junior, student",
                Location = "Remote · Global",
                OpportunityType = "Individual developer",
                PublishedWithinDays = 14,
                SelectedSources = new List<string> { "Company careers", "Public job board" },
                MaxResults = 40,
                IsActive = true,
                CreatedBy = "System",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-3),
                UpdatedAtUtc = DateTime.UtcNow.AddHours(-12)
            }
        };

        foreach (var p in seed)
        {
            Profiles[p.Id] = p;
        }
    }

    public Task<IReadOnlyList<SearchProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SearchProfile> list = Profiles.Values.OrderByDescending(p => p.UpdatedAtUtc).ToList();
        return Task.FromResult(list);
    }

    public Task<SearchProfile?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        Profiles.TryGetValue(id, out var profile);
        return Task.FromResult(profile);
    }

    public Task<SearchProfile> CreateAsync(SearchProfile profile, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            profile.Id = $"PRF-{1000 + Profiles.Count + 1}";
        }
        Profiles[profile.Id] = profile;
        return Task.FromResult(profile);
    }

    public Task<SearchProfile?> UpdateAsync(SearchProfile profile, CancellationToken cancellationToken = default)
    {
        if (Profiles.ContainsKey(profile.Id))
        {
            Profiles[profile.Id] = profile;
            return Task.FromResult<SearchProfile?>(profile);
        }
        return Task.FromResult<SearchProfile?>(null);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var removed = Profiles.TryRemove(id, out _);
        return Task.FromResult(removed);
    }
}
