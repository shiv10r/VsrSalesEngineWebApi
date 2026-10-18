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
                Id = "OP-1048",
                Company = "Northstar Digital",
                Title = "Senior .NET / React Engineer",
                Source = "Company careers",
                Location = "Remote · India",
                FoundAt = "Today, 10:42 AM",
                Status = "New",
                Fit = "High",
                Type = "Individual developer",
                HasVendorDocRequirements = false,
                SourceUrl = "https://example.com/careers/northstar-dotnet",
                EvidenceSnippet = "Looking for a Senior .NET Core / React engineer with 5+ years building scalable B2B APIs.",
                DeduplicationKey = "northstar digital|senior .net / react engineer",
                RetrievedAtUtc = DateTime.UtcNow.AddMinutes(-45),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-45),
                StatusHistory = new List<OpportunityStatusChange>
                {
                    new() { FromStatus = "None", ToStatus = "New", ChangedBy = "Discovery Worker", Reason = "Initial harvest", ChangedAtUtc = DateTime.UtcNow.AddMinutes(-45) }
                }
            },
            new Opportunity
            {
                Id = "OP-1047",
                Company = "CloudHarbor Labs",
                Title = "Contract API modernization partner",
                Source = "Public job board",
                Location = "Bengaluru, India",
                FoundAt = "Today, 9:18 AM",
                Status = "Needs review",
                Fit = "High",
                Type = "Company/vendor",
                HasVendorDocRequirements = true,
                VendorRequirementNotes = "Notice states supplier onboarding registration pack required prior to vendor selection.",
                SourceUrl = "https://example.com/jobs/cloudharbor-modernization",
                EvidenceSnippet = "Seeking software vendor partner for legacy ASP.NET migration to modern cloud architecture.",
                DeduplicationKey = "cloudharbor labs|contract api modernization partner",
                RetrievedAtUtc = DateTime.UtcNow.AddHours(-2),
                UpdatedAtUtc = DateTime.UtcNow.AddHours(-2),
                StatusHistory = new List<OpportunityStatusChange>
                {
                    new() { FromStatus = "New", ToStatus = "Needs review", ChangedBy = "Sales Member", Reason = "Vendor documentation requirement identified", ChangedAtUtc = DateTime.UtcNow.AddHours(-2) }
                }
            },
            new Opportunity
            {
                Id = "OP-1046",
                Company = "BrightPath Health",
                Title = "Healthcare platform engineering support",
                Source = "Company website",
                Location = "Remote · Global",
                FoundAt = "Yesterday",
                Status = "Qualified",
                Fit = "High",
                Type = "Direct project",
                HasVendorDocRequirements = false,
                SourceUrl = "https://example.com/careers/brightpath-platform",
                EvidenceSnippet = "Direct contracting opportunity for engineering team to expand healthcare analytics pipeline.",
                DeduplicationKey = "brightpath health|healthcare platform engineering support",
                RetrievedAtUtc = DateTime.UtcNow.AddDays(-1),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1),
                StatusHistory = new List<OpportunityStatusChange>
                {
                    new() { FromStatus = "New", ToStatus = "Qualified", ChangedBy = "VSR Admin", Reason = "Engineering skills and scope align with core delivery team", ChangedAtUtc = DateTime.UtcNow.AddDays(-1) }
                }
            },
            new Opportunity
            {
                Id = "OP-1045",
                Company = "Vertex Logistics",
                Title = "Full-stack .NET developer contract",
                Source = "Approved feed",
                Location = "Gurugram, India",
                FoundAt = "Yesterday",
                Status = "Contacted",
                Fit = "Medium",
                Type = "Individual developer",
                HasVendorDocRequirements = false,
                SourceUrl = "https://example.com/feed/vertex-contract",
                EvidenceSnippet = "6-month contract for full-stack developer experienced in ASP.NET Core, EF Core and TypeScript.",
                DeduplicationKey = "vertex logistics|full-stack .net developer contract",
                RetrievedAtUtc = DateTime.UtcNow.AddDays(-1),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            new Opportunity
            {
                Id = "OP-1044",
                Company = "NimbusWorks",
                Title = "Build internal operations portal",
                Source = "Public procurement",
                Location = "New Delhi, India",
                FoundAt = "Oct 16",
                Status = "Readiness hold",
                Fit = "Medium",
                Type = "Company/vendor",
                HasVendorDocRequirements = true,
                VendorRequirementNotes = "Requires GST certificate, registered office proof, and 3 years audited financials.",
                SourceUrl = "https://example.com/procurement/nimbus-portal",
                EvidenceSnippet = "Notice for supplier empanelment to build workflow portal; vendor tax credentials needed.",
                DeduplicationKey = "nimbusworks|build internal operations portal",
                RetrievedAtUtc = DateTime.UtcNow.AddDays(-2),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            new Opportunity
            {
                Id = "OP-1043",
                Company = "Asterloop Systems",
                Title = "React frontend contractor",
                Source = "Company careers",
                Location = "Remote · India",
                FoundAt = "Oct 16",
                Status = "Interested",
                Fit = "High",
                Type = "Individual developer",
                HasVendorDocRequirements = false,
                SourceUrl = "https://example.com/careers/asterloop-react",
                EvidenceSnippet = "Contractor needed to revamp client-facing dashboard using React and Tailwind.",
                DeduplicationKey = "asterloop systems|react frontend contractor",
                RetrievedAtUtc = DateTime.UtcNow.AddDays(-2),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
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
