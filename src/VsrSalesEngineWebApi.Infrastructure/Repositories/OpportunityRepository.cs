using System.Collections.Concurrent;
using VsrSalesEngineWebApi.Core.DTOs;
using VsrSalesEngineWebApi.Core.Entities;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Infrastructure.Repositories;

public class OpportunityRepository : IOpportunityRepository
{
    private static readonly ConcurrentDictionary<string, Opportunity> Store = new(StringComparer.OrdinalIgnoreCase);

    static OpportunityRepository()
    {
        var seeds = new[]
        {
            new Opportunity
            {
                Id = "OP-1001",
                Company = "Northstar Digital",
                Title = "Senior .NET / React Engineer",
                Source = "Company careers",
                Location = "Remote · India",
                FoundAt = "Today, 10:42 AM",
                Status = "Qualified",
                Fit = "High",
                Type = "Direct project",
                HasVendorDocRequirements = true,
                VendorRequirementNotes = "Verified supplier onboarding registration and technical requirements.",
                SourceUrl = "https://example.com/careers/northstar-dotnet",
                EvidenceSnippet = "Looking for a Senior .NET Core / React engineer with 5+ years building scalable B2B APIs.",
                DeduplicationKey = "northstar digital|senior .net / react engineer",
                RetrievedAtUtc = DateTime.UtcNow.AddMinutes(-45),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-45),
                StatusHistory = new List<OpportunityStatusChange>
                {
                    new() { FromStatus = "New", ToStatus = "Qualified", ChangedBy = "VSR Admin", Reason = "Technical requirements and budget verified", ChangedAtUtc = DateTime.UtcNow.AddMinutes(-45) }
                }
            }
        };

        foreach (var s in seeds)
        {
            Store[s.Id] = s;
        }
    }

    public Task<PagedResultDto<Opportunity>> GetPagedAsync(OpportunityFilterDto filter, CancellationToken cancellationToken = default)
    {
        var filtered = FilterOpportunities(filter).ToList();
        var total = filtered.Count;
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResultDto<Opportunity>(paged, total, page, pageSize, totalPages));
    }

    public Task<IReadOnlyList<Opportunity>> GetAllAsync(OpportunityFilterDto? filter = null, CancellationToken cancellationToken = default)
    {
        var results = FilterOpportunities(filter ?? new OpportunityFilterDto(null, null, null, null, null)).ToList();
        return Task.FromResult<IReadOnlyList<Opportunity>>(results);
    }

    public Task<Opportunity?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        Store.TryGetValue(id, out var opp);
        return Task.FromResult(opp);
    }

    public Task<Opportunity?> GetByDeduplicationKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var opp = Store.Values.FirstOrDefault(o => string.Equals(o.DeduplicationKey, key, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(opp);
    }

    public Task<Opportunity> AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        Store[opportunity.Id] = opportunity;
        return Task.FromResult(opportunity);
    }

    public Task<Opportunity?> UpdateAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        Store[opportunity.Id] = opportunity;
        return Task.FromResult<Opportunity?>(opportunity);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var removed = Store.TryRemove(id, out _);
        return Task.FromResult(removed);
    }

    private static IEnumerable<Opportunity> FilterOpportunities(OpportunityFilterDto filter)
    {
        IEnumerable<Opportunity> query = Store.Values;

        if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            var term = filter.SearchQuery.Trim().ToLowerInvariant();
            query = query.Where(o =>
                o.Company.ToLowerInvariant().Contains(term) ||
                o.Title.ToLowerInvariant().Contains(term) ||
                o.Source.ToLowerInvariant().Contains(term) ||
                o.Location.ToLowerInvariant().Contains(term) ||
                o.Id.ToLowerInvariant().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) && !string.Equals(filter.Status, "All statuses", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => string.Equals(o.Status, filter.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Source) && !string.Equals(filter.Source, "All sources", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => string.Equals(o.Source, filter.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Fit) && !string.Equals(filter.Fit, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(o => string.Equals(o.Fit, filter.Fit, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.HasVendorDocs.HasValue)
        {
            query = query.Where(o => o.HasVendorDocRequirements == filter.HasVendorDocs.Value);
        }

        return query.OrderByDescending(o => o.RetrievedAtUtc);
    }
}
